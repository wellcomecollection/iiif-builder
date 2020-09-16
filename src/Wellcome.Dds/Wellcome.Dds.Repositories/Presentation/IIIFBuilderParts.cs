using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.Auth;
using IIIF.ImageApi.Service;
using IIIF.Presentation;
using IIIF.Presentation.Annotation;
using IIIF.Presentation.Constants;
using IIIF.Presentation.Content;
using IIIF.Presentation.Strings;
using IIIF.Search;
using Microsoft.EntityFrameworkCore.Storage;
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
using Wellcome.Dds.Repositories.WordsAndPictures;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;
using Range = IIIF.Presentation.Range;


namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilderParts
    {
        private readonly UriPatterns uriPatterns;
        private readonly int dlcsDefaultSpace;
        private readonly ManifestStructureHelper manifestStructureHelper;
        private readonly IAuthServiceProvider authServiceProvider;

        private readonly IService clickthroughService;
        private readonly IService clickthroughServiceReference;
        private readonly IService loginService;
        private readonly IService loginServiceReference;
        private readonly IService externalAuthService;
        private readonly IService externalAuthServiceReference;
        
        public IIIFBuilderParts(
            UriPatterns uriPatterns,
            int dlcsDefaultSpace)
        {
            this.uriPatterns = uriPatterns;
            this.dlcsDefaultSpace = dlcsDefaultSpace;
            manifestStructureHelper = new ManifestStructureHelper();
            authServiceProvider = new DlcsIIIFAuthServiceProvider();
            
            // These still bear lots of traces of previous incarnations
            // We only want one of these but I'm not quite sure which one...
            clickthroughService = authServiceProvider.GetAcceptTermsAuthServices().First();
            clickthroughServiceReference = new ServiceReference(clickthroughService);
            loginService = authServiceProvider.GetClinicalLoginServices().First();
            loginServiceReference = new ServiceReference(loginService);
            externalAuthService = authServiceProvider.GetRestrictedLoginServices().First();
            externalAuthServiceReference = new ServiceReference(externalAuthService);
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
            var foundAuthServices = new Dictionary<string, IService>();
            var manifestIdentifier = digitisedManifestation.MetsManifestation.Id;
            manifest.Items = new List<Canvas>();
            foreach (var physicalFile in digitisedManifestation.MetsManifestation.SignificantSequence)
            {
                string orderLabel = null;
                LanguageMap canvasLabel = null;
                if (physicalFile.OrderLabel.HasText())
                {
                    orderLabel = physicalFile.OrderLabel;
                    canvasLabel = Lang.Map("none", orderLabel);
                }
                var canvas = new Canvas
                {
                    Id = uriPatterns.Canvas(manifestIdentifier, physicalFile.StorageIdentifier),
                    Label = canvasLabel
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
                            canvas.Label = Lang.Map("Closed");
                            canvas.Summary = Lang.Map("This image is not currently available online");
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
                            canvas.SeeAlso = new List<ExternalResource>
                            {
                                new ExternalResource("Dataset")
                                {
                                    Id = uriPatterns.MetsAlto(manifestIdentifier, assetIdentifier),
                                    Format = "text/html",
                                    Profile = "http://www.loc.gov/standards/alto/v3/alto.xsd",
                                    Label = Lang.Map("none", "METS-ALTO XML")
                                }
                            };
                            canvas.Annotations = new List<AnnotationPage>
                            {
                                new AnnotationPage
                                {
                                    Id = uriPatterns.CanvasOtherAnnotationPage(manifestIdentifier, assetIdentifier),
                                    Label = Lang.Map(orderLabel.HasText() ? $"Text of page {orderLabel}" : "Text of this page")
                                }
                            };
                        }
                        AddAuthServices(mainImage, physicalFile, foundAuthServices);
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

            if (foundAuthServices.HasItems())
            {
                manifest.Services = foundAuthServices.Values.ToList();
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
        
        
        private void AddAuthServices(
            Image mainImage, 
            IPhysicalFile physicalFile,
            Dictionary<string, IService> foundAuthServices)
        {
            switch (physicalFile.AccessCondition)
            {
                case AccessCondition.Open:
                    // no auth services needed, we're open and happy.
                    return;
                case AccessCondition.RequiresRegistration: // i.e., Clickthrough
                    AddAuthServiceToDictionary(foundAuthServices, clickthroughService);
                    AddAuthServiceToImage(mainImage, clickthroughServiceReference);
                    break;
                case AccessCondition.ClinicalImages: // i.e., Login (IIIF standard auth)
                case AccessCondition.Degraded:
                    AddAuthServiceToDictionary(foundAuthServices, loginService);
                    AddAuthServiceToImage(mainImage, loginServiceReference);
                    break;
                case AccessCondition.RestrictedFiles: // i.e., IIIF external auth
                    AddAuthServiceToDictionary(foundAuthServices, externalAuthService);
                    AddAuthServiceToImage(mainImage, externalAuthServiceReference);
                    break;
                default:
                    throw new NotImplementedException("Unknown access condition " + physicalFile.AccessCondition);
            }
        }

        private void AddAuthServiceToDictionary(Dictionary<string, IService> foundAuthServices, IService service)
        {
            if (!foundAuthServices.ContainsKey(service.Id))
            {
                foundAuthServices[service.Id] = service;
            }
        }
        
        private void AddAuthServiceToImage(Image image, IService service)
        {
            if (image == null || service == null)
            {
                return;
            }

            if (image.Service.HasItems())
            {
                var iiifImageApi2 = (ImageService2) image.Service.First();
                iiifImageApi2.Service = new List<IService>{service};
                image.Service.Add(service);
            }
            else
            {
                image.Service = new List<IService>{service};
            }
            
        }


        public void ServicesForAuth(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            if (!manifest.Items.HasItems())
            {
                throw new NotSupportedException("Please build the canvases first, then call this!");
            }
    
            // TODO - this is a bit wasteful... we put full auth services on all the images and services, then we took them off again.
            // find all the distinct auth services in the images on the canvases,
            // and then add them to the manifest-level services property,
            // leaving just a reference at the canvas level
            Dictionary<string, IService> distinctAuthServices;
            foreach (var canvas in manifest.Items)
            {
                var paintingAnno = canvas.Items?.FirstOrDefault()?.Items?.FirstOrDefault();
                if (paintingAnno != null)
                {
                    var resource = ((PaintingAnnotation) paintingAnno).Body;
                    if (resource != null && resource.Service.HasItems())
                    {
                        
                    }
                }

            }
        }

        public void Structures(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            var metsManifestation = digitisedManifestation.MetsManifestation;
            
            var physIdDict = metsManifestation.SignificantSequence.ToDictionary(
                pf => pf.Id, pf => pf.StorageIdentifier);
            
            // See MetsRepositoryPackageProvider, line 379, and https://digirati.atlassian.net/browse/WDL-97
            var wdlRoot = GetSectionFromStructDiv(
                metsManifestation.Id,
                physIdDict,
                metsManifestation.RootStructRange, 
                metsManifestation.ParentModsData);
            if (IsManuscriptStructure(metsManifestation.RootStructRange))
            {
                wdlRoot = ConvertFirstChildToRoot(wdlRoot);
            }
            // we now have the equivalent of old DDS Section - but the rootsection IS the equivalent
            // of the manifest, which we already have. We don't need a top level Range for everything,
            // we're only interested in Child structure.
            if (wdlRoot != null && wdlRoot.Items.HasItems())
            {
                var topRanges = wdlRoot.Items.Where(r => r is Range).ToList();
                if (topRanges.HasItems())
                {
                    // These should all be ranges. I think. It's an error if they aren't?
                    // TEST TEST TEST...
                    manifest.Structures = topRanges.Cast<Range>().ToList();
                }
            }
        }

        private Range ConvertFirstChildToRoot(Range wdlRoot)
        {
            var newRoot = (Range) wdlRoot.Items?.FirstOrDefault(r => r is Range);
            if (newRoot == null) return wdlRoot;
            newRoot.Label = wdlRoot.Label;
            return newRoot;
        }

        private bool IsManuscriptStructure(IStructRange rootStructRange)
        {
            // return (
            //     rootSection != null
            //     && rootSection.SectionType == "Archive"
            //     && rootSection.Sections != null
            //     && rootSection.Sections.Length == 1
            //     && rootSection.Sections[0].SectionType == "Manuscript"
            //     && rootSection.Sections[0].Assets.Length == rootSection.Assets.Length
            // );
            return (
                rootStructRange != null
                && rootStructRange.Type == "Archive"
                && rootStructRange.Children.HasItems()
                && rootStructRange.Children.Count == 1
                && rootStructRange.Children[0].Type == "Manuscript"
                && rootStructRange.Children[0].PhysicalFileIds.Count == rootStructRange.PhysicalFileIds.Count
            );
            
        }

        private Range GetSectionFromStructDiv(
            string manifestationId,
            Dictionary<string, string> physIdDict,
            IStructRange structRange,
            IModsData parentMods)
        {
            var range = new Range
            {
                Id = uriPatterns.Range(manifestationId, structRange.Id)
            };
            if (structRange.Type == "PeriodicalIssue" && parentMods != null)
            {
                // for periodicals, some MODS data is held at the VOLUME level, 
                // which is the dmdSec referenced by the parent structural div
                MergeExtraPeriodicalVolumeData(structRange.Mods, parentMods);
            }

            var modsForAccessCondition = structRange.Mods ?? parentMods;
            if (!range.Label.HasItems()) // && structRange.Mods != null)
            {
                range.Label = GetMappedRangeLabel(structRange);
            }
            
            
            // physIdDict contains the "significant" assets; we should only add these, not all the assets
            var canvases = new List<Canvas>(); // this was called sectionAssets, int list
            foreach (string physicalFileId in structRange.PhysicalFileIds)
            {
                string storageIdentifier = null;
                if (physIdDict.TryGetValue(physicalFileId, out storageIdentifier))
                {
                    canvases.Add(new Canvas
                    {
                        Id = uriPatterns.Canvas(manifestationId, storageIdentifier)
                    });
                }
            }

            if (canvases.HasItems())
            {
                range.Items ??= new List<IStructuralLocation>();
                range.Items.AddRange(canvases);
            }
            
            if (structRange.Children.HasItems())
            {
                var childRanges = structRange.Children
                    .Select(child => GetSectionFromStructDiv(manifestationId, physIdDict, child, modsForAccessCondition))
                    .ToList();
                if (childRanges.HasItems())
                {
                    range.Items ??= new List<IStructuralLocation>();
                    range.Items.AddRange(childRanges);
                }
            }
            
            return range;
        }

        private LanguageMap GetMappedRangeLabel(IStructRange structRange)
        {
            var s = structRange.Mods?.Title ?? structRange.Type;
            var humanFriendly = manifestStructureHelper.GetHumanFriendlySectionLabel(s);
            return Lang.Map("none", humanFriendly); // TODO - "en" is often wrong.
        }
        
        /// <summary>
        /// For Periodicals, additional MODS data (typically security info) is carried in a different
        /// MODS section, so we need to incorporate it into the current section's MODS
        /// </summary>
        /// <param name="sectionMods">The MODS for the current structural section - the periodical issue</param>
        /// <param name="volumeMods">The MODS for the volume (one per METS file)</param>
        private void MergeExtraPeriodicalVolumeData(IModsData sectionMods, IModsData volumeMods)
        {
            if (sectionMods.AccessCondition.IsNullOrWhiteSpace() || sectionMods.AccessCondition == "Open")
            {
                sectionMods.AccessCondition = volumeMods.AccessCondition;
            }
            if (sectionMods.DzLicenseCode.IsNullOrWhiteSpace())
            {
                sectionMods.DzLicenseCode = volumeMods.DzLicenseCode;
                if (sectionMods.DzLicenseCode.IsNullOrWhiteSpace())
                {
                    sectionMods.DzLicenseCode = "CC-BY-NC";
                }
            }
            if (sectionMods.PlayerOptions <= 0)
            {
                sectionMods.PlayerOptions = volumeMods.PlayerOptions;
            }
            if (sectionMods.RecordIdentifier.IsNullOrWhiteSpace())
            {
                sectionMods.RecordIdentifier = volumeMods.RecordIdentifier;
            }
        }


        // ^^ move to structurehelper?
        
        
        
        
        
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