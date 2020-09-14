using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.Auth;
using IIIF.Presentation;
using IIIF.Presentation.Annotation;
using IIIF.Presentation.Constants;
using IIIF.Presentation.Content;
using IIIF.Presentation.Strings;
using IIIF.Search;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.AuthServices;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;


namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilderParts
    {
        private readonly UriPatterns uriPatterns;
        private readonly int dlcsDefaultSpace;
        private readonly ManifestStructureHelper manifestStructureHelper;
        private readonly IAuthServiceProvider authServiceProvider;

        private readonly List<IService> clickthroughServices;
        private readonly List<IService> loginServices;
        private readonly List<IService> externalAuthServices;
        
        public IIIFBuilderParts(
            UriPatterns uriPatterns,
            int dlcsDefaultSpace)
        {
            this.uriPatterns = uriPatterns;
            this.dlcsDefaultSpace = dlcsDefaultSpace;
            manifestStructureHelper = new ManifestStructureHelper();
            authServiceProvider = new DlcsIIIFAuthServiceProvider();
            
            // These still bear lots of traces of previous incarnations
            clickthroughServices = authServiceProvider.GetAcceptTermsAuthServices();
            loginServices = authServiceProvider.GetClinicalLoginServices();
            externalAuthServices = authServiceProvider.GetRestrictedLoginServices();
        }


        public void HomePage(ResourceBase iiifResource, Work work)
        {
            iiifResource.Homepage = new List<ExternalResource>
            {
                new ExternalResource("Text")
                {
                    Id = uriPatterns.PersistentPlayerUri(work.Id),
                    Label = Lang.Map(work.Title),
                    Format = "text/html",
                    Language = new List<string>{"en"}
                }
            };
        }

        public void Aggregations(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var groups = manifestationMetadata.Metadata.GroupBy(m => m.Label);
            foreach (var @group in groups)
            {
                foreach (var md in @group)
                {
                    iiifResource.PartOf ??= new List<ResourceBase>();
                    iiifResource.PartOf.Add(
                        new Collection
                        {
                            Id = uriPatterns.CollectionForAggregation(md.Label, md.Identifier),
                            Label = new LanguageMap("en", $"{md.Label}: {md.StringValue}")
                        });
                }
            }
        }


        public void SeeAlso(ResourceBase iiifResource, Work work)
        {
            iiifResource.SeeAlso = new List<ExternalResource>
            {
                new ExternalResource("Dataset")
                {
                    Id = uriPatterns.CatalogueApi(work.Id, new string[]{}),
                    Label = Lang.Map("Wellcome Collection Catalogue API"),
                    Format = "application/json",
                    Profile = "https://api.wellcomecollection.org/catalogue/v2/context.json"
                }
            };
        }


        public void Summary(StructureBase iiifResource, Work work)
        {
            if (work.Description.HasText())
            {
                // Would this ever not be in English?
                iiifResource.Summary = Lang.Map(work.Description);
            }
        }

        public void RequiredStatement(
            Manifest manifest, 
            IDigitisedManifestation digitisedManifestation,
            ManifestationMetadata manifestationMetadata)
        {
            var usage = LicenceHelpers.GetUsageWithHtmlLinks(digitisedManifestation.MetsManifestation.ModsData.Usage);
            if (!usage.HasText())
            {
                var code = GetMappedLicenceCode(digitisedManifestation);
                if (code.HasText())
                {
                    var dict = PlayerConfigProvider.BaseConfig.Modules.ConditionsDialogue.Content;
                    usage = dict.ContainsKey(code) ? dict[code] : null;
                    if (usage != null && !usage.StartsWith("<"))
                    {
                        // Make HTML
                        usage = usage.WrapSpan();
                    }
                }
            }

            var attribution = "Wellcome Collection";
            var locationOfOriginal = manifestationMetadata.Metadata.GetLocationOfOriginal();
            if (locationOfOriginal.HasText())
            {
                attribution =
                    $"This material has been provided by {locationOfOriginal} where the originals may be consulted.";
            }

            if (StringUtils.AnyHaveText(usage, attribution))
            {
                const string label = "Attribution and usage";
                manifest.RequiredStatement = new LabelValuePair("en", label, attribution, usage);
            }
            // TODO - what do we want to do with this?
            // var permittedOps = digitisedManifestation.MetsManifestation.PermittedOperations;
            // var accessCondition = digitisedManifestation.MetsManifestation.ModsData.AccessCondition;
        }

        public void Rights(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var code = GetMappedLicenceCode(digitisedManifestation);
            var uri = LicenseMap.GetLicenseUri(code);
            if (uri.HasText())
            {
                manifest.Rights = uri;
            }
        }

        private static string GetMappedLicenceCode(IDigitisedManifestation digitisedManifestation)
        {
            var dzl = digitisedManifestation.MetsManifestation.ModsData.DzLicenseCode;
            return LicenceCodes.MapLicenseCode(dzl);
        }

        public void PagedBehavior(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var structType = digitisedManifestation.MetsManifestation.RootStructRange.Type;
            if (structType == "Monograph" || structType == "Manuscript")
            {
                manifest.Behavior ??= new List<string>();
                manifest.Behavior.Add(Behavior.Paged);
            }
        }

        public void ViewingDirection(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // Old DDS does not ever set this!
            // Not sure we ever have enough data to set it, but is something we can come back to later.
        }

        public void Rendering(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var permitted = digitisedManifestation.MetsManifestation.PermittedOperations;
            if (permitted.HasItems() && permitted.Contains("entireDocumentAsPdf"))
            {
                manifest.Rendering ??= new List<ExternalResource>();
                // At the moment, "Text" is not really a good Type for the PDF - but what else?
                manifest.Rendering.Add(new ExternalResource("Text")
                {
                    // TODO - are space and identifier the right way round, in the new query?
                    Id = uriPatterns.DlcsPdf(dlcsDefaultSpace, digitisedManifestation.Identifier),
                    Label = Lang.Map("View as PDF"),
                    Format = "application/pdf"
                });
            }

            if (digitisedManifestation.MetsManifestation.SignificantSequence.SupportsSearch())
            {
                manifest.Rendering ??= new List<ExternalResource>();
                manifest.Rendering.Add(new ExternalResource("Text")
                {
                    Id = uriPatterns.RawText(digitisedManifestation.Identifier),
                    Label = Lang.Map("View raw text"),
                    Format = "text/plain"
                });
            }
        }

        public void SearchServices(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            if (digitisedManifestation.MetsManifestation.SignificantSequence.SupportsSearch())
            {
                manifest.Service ??= new List<IService>();
                manifest.Service.Add(new SearchService2
                {
                    Id = uriPatterns.IIIFContentSearchService2(digitisedManifestation.Identifier),
                    Label = Lang.Map("Search within this manifest"),
                    Service = new List<IService>
                    {
                        new AutoCompleteService2
                        {
                            Id = uriPatterns.IIIFAutoCompleteService2(digitisedManifestation.Identifier),
                            Label = Lang.Map("Autocomplete words in this manifest")
                        }
                    }
                });
            }
        }
        
        public void Canvases(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var manifestIdentifier = digitisedManifestation.MetsManifestation.Id;
            manifest.Items = new List<Canvas>();
            foreach (var physicalFile in digitisedManifestation.MetsManifestation.SignificantSequence)
            {
                var canvas = new Canvas
                {
                    Id = uriPatterns.Canvas(manifestIdentifier, physicalFile.StorageIdentifier),
                    Label = Lang.Map("none", physicalFile.OrderLabel)
                };
                manifest.Items.Add(canvas);
                var assetIdentifier = physicalFile.StorageIdentifier;
                switch (physicalFile.Family)
                {
                    case AssetFamily.Image:
                        var size = new Size(
                            physicalFile.AssetMetadata.GetImageWidth(),
                            physicalFile.AssetMetadata.GetImageHeight());
                        canvas.Width = size.Width;
                        canvas.Height = size.Height;
                        if (physicalFile.ExcludeDlcsAssetFromManifest())
                        {
                            // has an unknown or forbidden access condition
                            break;
                        }
                        var (mainImage, thumbImage) = GetCanvasImages(physicalFile);
                        canvas.Items = new List<AnnotationPage>
                        {
                            new AnnotationPage
                            {
                                Id = uriPatterns.CanvasPaintingAnnotationPage(manifestIdentifier, assetIdentifier),
                                Items = new List<IAnnotation>
                                {
                                    new PaintingAnnotation
                                    {
                                        Id = uriPatterns.CanvasPaintingAnnotation(manifestIdentifier, assetIdentifier),
                                        Body = mainImage
                                    }
                                }
                            }
                        };
                        if (thumbImage != null)
                        {
                            canvas.Thumbnail = new List<ExternalResource> { thumbImage };
                        }
                        if (physicalFile.RelativeAltoPath.HasText())
                        {
                            // add ALTO seeAlso
                            // add granular anno list
                        }
                        AddAuthServices(mainImage, physicalFile);
                        break;
                    case AssetFamily.TimeBased:
                        // TODO - we need to sort this out properly
                        // We need an accurate time measure back from the DLCS, and not use catalogue metadata
                        // canvas.Duration = physicalFile.AssetMetadata.GetLengthInSeconds().ParseDuration();
                        break;
                    case AssetFamily.File:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void ImprovePagingSequence(Manifest manifest)
        {
            manifestStructureHelper.ImprovePagingSequence(manifest);
        }


        private (Image MainImage, Image ThumbImage) GetCanvasImages(IPhysicalFile physicalFile)
        {
            var assetIdentifier = physicalFile.StorageIdentifier;
            var imageService = uriPatterns.DlcsImageService(dlcsDefaultSpace, assetIdentifier);
            var thumbService = uriPatterns.DlcsThumb(dlcsDefaultSpace, assetIdentifier);
            var sizes = physicalFile.GetAvailableSizes();
            var actualSize = sizes.First();
            var thumbSizes = sizes.Skip(1).ToList();
            var staticSize = actualSize;
            Image thumbImage = null;
            if (thumbSizes.Any())
            {
                staticSize = thumbSizes.First();
                thumbImage = thumbService.AsThumbnailWithService(thumbSizes);
            }
            var mainImage = imageService.AsImageWithService(actualSize, staticSize);
            return (mainImage, thumbImage);
        }
        
        
        private void AddAuthServices(Image mainImage, IPhysicalFile physicalFile)
        {
            switch (physicalFile.AccessCondition)
            {
                case AccessCondition.Open:
                    // no auth services needed, we're open and happy.
                    return;
                case AccessCondition.RequiresRegistration: // i.e., Clickthrough
                    mainImage.Service = clickthroughServices;
                    break;
                case AccessCondition.ClinicalImages: // i.e., Login (IIIF standard auth)
                    mainImage.Service = loginServices;
                    break;
                case AccessCondition.RestrictedFiles: // i.e., IIIF external auth
                    mainImage.Service = externalAuthServices;
                    break;
                    
                
            }
            if (physicalFile.AccessCondition == AccessCondition.Open)
            {
                
            }
            
        }

        public void ServicesForAuth(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            if (!manifest.Items.HasItems())
            {
                throw new NotSupportedException("Please build the canvases first, then call this!");
            }
            // find all the distinct auth services in the images on the canvases,
            // and then add them to the manifest-level services property,
            // leaving just a reference at the canvas level
        }

        public void Structures(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        public void ManifestLevelAnnotations(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }


        public void Metadata(ResourceBase iiifResource, Work work)
        {
            // throw new NotImplementedException();
        }

        public void ArchiveCollectionStructure(ResourceBase iiifResource, Work work)
        {
            // throw new NotImplementedException();
        }
    }
}