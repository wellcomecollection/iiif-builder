using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V1;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.AuthServices;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using Wellcome.Dds.Repositories.Presentation.V2.IXIF;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;
using Range = IIIF.Presentation.V3.Range;
using StringUtils = Utils.StringUtils;
using Manifest = IIIF.Presentation.V3.Manifest;
using Collection = IIIF.Presentation.V3.Collection;
using Canvas = IIIF.Presentation.V3.Canvas;
using ExternalResource = IIIF.Presentation.V3.Content.ExternalResource;
using ResourceBase = IIIF.Presentation.V3.ResourceBase;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilderParts
    {
        private readonly UriPatterns uriPatterns;
        private readonly string dlcsEntryPoint;
        private readonly bool referenceV0SearchService;
        private readonly ManifestStructureHelper manifestStructureHelper;
        private readonly IAuthServiceProvider authServiceProvider;

        private readonly IService clickthroughService;
        private readonly IService clickthroughServiceReference;
        private readonly IService loginService;
        private readonly IService loginServiceReference;
        private readonly IService externalAuthService;
        private readonly IService externalAuthServiceReference;

        // omit Digitalcollection and Location
        private static readonly string[] DisplayedAggregations = {"Genre", "Subject", "Contributor"};
        
        public IIIFBuilderParts(UriPatterns uriPatterns,
            string dlcsEntryPoint,
            bool referenceV0SearchService)
        {
            this.uriPatterns = uriPatterns;
            this.dlcsEntryPoint = dlcsEntryPoint;
            this.referenceV0SearchService = referenceV0SearchService;
            manifestStructureHelper = new ManifestStructureHelper();
            authServiceProvider = new DlcsIIIFAuthServiceProvider();
            
            // These still bear lots of traces of previous incarnations
            // We only want one of these but I'm not quite sure which one...
            clickthroughService = authServiceProvider.GetAcceptTermsAuthServices().First();
            clickthroughServiceReference = new V2ServiceReference(clickthroughService);
            loginService = authServiceProvider.GetClinicalLoginServices().First();
            loginServiceReference = new V2ServiceReference(loginService);
            externalAuthService = authServiceProvider.GetRestrictedLoginServices().First();
            externalAuthServiceReference = new V2ServiceReference(externalAuthService);
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
                    if (!DisplayedAggregations.Contains(md.Label))
                    {
                        // don't use Location or DigitalCollection as memebership aggregations
                        continue;
                    }
                    var urlFriendlyAggregator = Wellcome.Dds.Metadata.ToUrlFriendlyAggregator(md.Label);
                    iiifResource.PartOf ??= new List<ResourceBase>();
                    iiifResource.PartOf.Add(
                        new Collection
                        {
                            Id = uriPatterns.CollectionForAggregation(urlFriendlyAggregator, md.Identifier),
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
                    Id = uriPatterns.CatalogueApi(work.Id),
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

        public void RequiredStatement(Manifest manifest,
            IManifestation metsManifestation,
            ManifestationMetadata manifestationMetadata,
            bool useRequiredStatement)
        {
            var usage = LicenceHelpers.GetUsageWithHtmlLinks(metsManifestation.ModsData.Usage);
            if (!usage.HasText())
            {
                var code = GetMappedLicenceCode(metsManifestation);
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

            // NOTE - the PDF uses the first string that is not "Wellcome Collection" for output
            var attribution = Constants.WellcomeCollection;
            var locationOfOriginal = manifestationMetadata.Metadata.GetLocationOfOriginal();
            if (locationOfOriginal.HasText())
            {
                attribution =
                    $"This material has been provided by {locationOfOriginal} where the originals may be consulted.";
            }

            if (StringUtils.AnyHaveText(usage, attribution))
            {
                var attributionAndUsage = new LabelValuePair("en", 
                    Constants.AttributionAndUsage, attribution, usage);
                if (useRequiredStatement)
                {
                    manifest.RequiredStatement = attributionAndUsage;
                }
                else
                {
                    manifest.Metadata ??= new List<LabelValuePair>();
                    manifest.Metadata.Add(attributionAndUsage);
                }
            }
            // TODO - what do we want to do with this?
            // Park for now and resurrect later, it all depends on what the wc.org front end wants to do with these things
            // var permittedOps = digitisedManifestation.MetsManifestation.PermittedOperations;
            // var accessCondition = digitisedManifestation.MetsManifestation.ModsData.AccessCondition;
        }

        public void Rights(Manifest manifest, IManifestation metsManifestation)
        {
            var code = GetMappedLicenceCode(metsManifestation);
            var uri = LicenseMap.GetLicenseUri(code);
            if (uri.HasText())
            {
                // the machine-readable versions use http IDs 
                uri = uri.Replace("https://creativecommons.org/", "http://creativecommons.org/");
                uri = uri.Replace("https://rightsstatements.org/", "http://rightsstatements.org/");
                manifest.Rights = uri;
            }
        }

        private static string GetMappedLicenceCode(IManifestation metsManifestation)
        {
            var dzl = metsManifestation.ModsData.DzLicenseCode;
            return LicenceCodes.MapLicenseCode(dzl);
        }

        public void PagedBehavior(Manifest manifest, IManifestation metsManifestation)
        {
            var structType = metsManifestation.RootStructRange.Type;
            if (structType == "Monograph" || structType == "Manuscript")
            {
                manifest.Behavior ??= new List<string>();
                manifest.Behavior.Add(Behavior.Paged);
            }
        }

        public void ViewingDirection(Manifest manifest, IManifestation metsManifestation)
        {
            // Old DDS does not ever set this!
            // Not sure we ever have enough data to set it, but is something we can come back to later.
        }

        public void Rendering(Manifest manifest, IManifestation metsManifestation)
        {
            var mType = metsManifestation.Type;
            if (mType == "Video" || mType == "Audio")
            {
                return;
            }
            var permitted = metsManifestation.PermittedOperations;
            if (permitted.HasItems() && permitted.Contains("entireDocumentAsPdf"))
            {
                manifest.Rendering ??= new List<ExternalResource>();
                // At the moment, "Text" is not really a good Type for the PDF - but what else?
                manifest.Rendering.Add(new ExternalResource("Text")
                {
                    Id = uriPatterns.DlcsPdf(dlcsEntryPoint, metsManifestation.Id),
                    Label = Lang.Map("View as PDF"),
                    Format = "application/pdf"
                });
            }

            if (metsManifestation.Sequence.SupportsSearch())
            {
                manifest.Rendering ??= new List<ExternalResource>();
                manifest.Rendering.Add(new ExternalResource("Text")
                {
                    Id = uriPatterns.RawText(metsManifestation.Id),
                    Label = Lang.Map("View raw text"),
                    Format = "text/plain"
                });
            }
        }

        public void SearchServices(Manifest manifest, IManifestation metsManifestation)
        {
            if (metsManifestation.Sequence.SupportsSearch())
            {
                manifest.EnsureContext(SearchService.Search1Context);
                manifest.Service ??= new List<IService>();
                string searchServiceId;
                searchServiceId = referenceV0SearchService ? 
                    uriPatterns.IIIFContentSearchService0(metsManifestation.Id) : 
                    uriPatterns.IIIFContentSearchService1(metsManifestation.Id);
                manifest.Service.Add(new SearchService
                {
                    Id = searchServiceId,
                    Profile = SearchService.Search1Profile,
                    Label = new MetaDataValue("Search within this manifest"),
                    Service = new AutoCompleteService
                    {
                        Id = uriPatterns.IIIFAutoCompleteService1(metsManifestation.Id),
                        Profile = AutoCompleteService.AutoCompleteService1Profile,
                        Label = new MetaDataValue("Autocomplete words in this manifest")
                    }
                });
            }
        }
        
        public void Canvases(Manifest manifest, IManifestation metsManifestation, State state)
        {
            var foundAuthServices = new Dictionary<string, IService>();
            var manifestIdentifier = metsManifestation.Id;
            manifest.Items = new List<Canvas>();
            foreach (var physicalFile in metsManifestation.Sequence)
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
                if (physicalFile.ExcludeDlcsAssetFromManifest())
                {
                    // has an unknown or forbidden access condition
                    canvas.Label = Lang.Map("Closed");
                    canvas.Summary = Lang.Map("This image is not currently available online");
                    continue;
                }
                
                var assetIdentifier = physicalFile.StorageIdentifier;
                
                switch (physicalFile.Family)
                {
                    case AssetFamily.Image:
                        var size = new Size(
                            physicalFile.AssetMetadata.GetImageWidth(),
                            physicalFile.AssetMetadata.GetImageHeight());
                        canvas.Width = size.Width;
                        canvas.Height = size.Height;
                        var (mainImage, thumbImage) = GetCanvasImages(physicalFile);
                        canvas.Items = new List<AnnotationPage>
                        {
                            new()
                            {
                                Id = uriPatterns.CanvasPaintingAnnotationPage(manifestIdentifier, assetIdentifier),
                                Items = new List<IAnnotation>
                                {
                                    new PaintingAnnotation
                                    {
                                        Id = uriPatterns.CanvasPaintingAnnotation(manifestIdentifier, assetIdentifier),
                                        Body = mainImage,
                                        Target = new Canvas{ Id = canvas.Id }
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
                                new("Dataset")
                                {
                                    Id = uriPatterns.MetsAlto(manifestIdentifier, assetIdentifier),
                                    Format = "text/xml",
                                    Profile = "http://www.loc.gov/standards/alto/v3/alto.xsd",
                                    Label = Lang.Map("none", "METS-ALTO XML")
                                }
                            };
                            canvas.Annotations = new List<AnnotationPage>
                            {
                                new ()
                                {
                                    Id = uriPatterns.CanvasOtherAnnotationPageWithVersion(manifestIdentifier, assetIdentifier, 3),
                                    Label = Lang.Map(orderLabel.HasText() ? $"Text of page {orderLabel}" : "Text of this page")
                                }
                            };
                        }
                        AddAuthServices(mainImage, physicalFile, foundAuthServices);
                        break;
                    case AssetFamily.TimeBased:
                        if (state == null)
                        {
                            // We need this state to build OLD workflow videos, because we need to consider
                            // other manifestations within a multiple manifestation.
                            
                            // But we don't need this state to build a new workflow video, which all lives
                            // in a single manifestation.
                            // https://github.com/wellcomecollection/platform/issues/4788
                            
                            // So how do we know what kind of workflow this is?
                            // Does this IPhysicalFile have an explicit USE="ACCESS" file group?
                            // For now we will assume that this is a sign of the new workflow
                            var accessFile = physicalFile.Files.FirstOrDefault(f => f.Use == "ACCESS");
                            if(accessFile == null)
                            {
                                throw new IIIFBuildStateException(
                                    "State is required to build AV resources for OLD WORKFLOWS");
                            }
                        }
                        Size videoSize = null;
                        if (physicalFile.Type == "Video" || metsManifestation.Type == "Video")
                        {
                            videoSize = new Size(
                                physicalFile.AssetMetadata.GetImageWidth(),
                                physicalFile.AssetMetadata.GetImageHeight());
                        }
                        double duration = physicalFile.AssetMetadata.GetDuration();
                        // Without more support in Goobi we might have ended up with 0,0,0 here.
                        // So we'll fake some obvious sizes
                        if (videoSize != null)
                        {
                            if (videoSize.Width <= 0 || videoSize.Height <= 0)
                            {
                                videoSize = new Size(999, 999);
                            }

                            canvas.Width = videoSize.Width;
                            canvas.Height = videoSize.Height;
                        }
                        if (duration <= 0)
                        {
                            duration = 999.99;
                        }
                        canvas.Duration = duration;
                        var avChoice = GetAVChoice(metsManifestation, physicalFile, videoSize, duration);
                        if (avChoice.Items?.Count > 0)
                        {
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
                                            Body = avChoice.Items.Count == 1 ? avChoice.Items[0] : avChoice,
                                            Target = new Canvas {Id = canvas.Id}
                                        }
                                    }
                                }
                            };
                            AddAuthServices(avChoice, physicalFile, foundAuthServices);
                            // This is still the DDS-hosted poster image, for old and new AV workflows
                            AddPosterImage(manifest, assetIdentifier, manifestIdentifier);
                            var transcriptPdf = physicalFile.Files.FirstOrDefault(f => f.Use == "TRANSCRIPT");
                            if (transcriptPdf != null)
                            {
                                // A new workflow transcript for this AV file
                                AddSupplementingPdfToCanvas(manifestIdentifier, canvas, transcriptPdf, "transcript", "PDF Transcript");
                            }
                        }

                        if (state != null)
                        {
                            state.AVState ??= new AVState();
                            state.AVState.Canvases.Add(canvas); // is this enough? Map them, somehow?
                        }
                        break;
                        
                    case AssetFamily.File:
                        if (metsManifestation.Type == "Monograph")
                        {
                            // TODO: is this simple logic OK for every BD PDF? See what comparison tool reveals.
                            // This is a born digital PDF
                            var bornDigitalPdf = physicalFile.Files.FirstOrDefault();
                            if (bornDigitalPdf != null)
                            {
                                // A new workflow transcript for this AV file
                                AddSupplementingPdfToCanvas(manifestIdentifier, canvas, bornDigitalPdf, 
                                    "pdf", manifest.Label.ToString());
                                var pageCountMetadata = GetPageCountMetadata(physicalFile);
                                if (pageCountMetadata != null)
                                {
                                    manifest.Metadata ??= new List<LabelValuePair>();
                                    manifest.Metadata.Add(pageCountMetadata);
                                }
                                manifest.Behavior = null;
                                manifest.Thumbnail = new List<ExternalResource>
                                {
                                    new Image
                                    {
                                        Id = uriPatterns.PdfThumbnail(manifestIdentifier),
                                        Format = "image/jpeg"
                                    }
                                };
                            }
                        }
                        else
                        {
                            // An AV transcript. Note it down and add it to the AVState
                            state.FileState ??= new FileState();
                            state.FileState.FoundFiles.Add(physicalFile);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (foundAuthServices.HasItems())
            {
                manifest.Services ??= new List<IService>();
                manifest.Services.AddRange(foundAuthServices.Values);
            }
        }

        private LabelValuePair? GetPageCountMetadata(IPhysicalFile physicalFile)
        {
            var pageCount = physicalFile.AssetMetadata.GetNumberOfPages();
            if (pageCount > 0)
            {
                var label = Lang.Map("en", "Number of pages");
                var value = Lang.Map("none", pageCount.ToString());
                return new LabelValuePair(label, value);
            }

            return null;
        }

        private void AddPosterImage(Manifest manifest, string assetIdentifier, string manifestIdentifier)
        {
            var posterAssetIdentifier = $"poster-{assetIdentifier}";
            var posterCanvasId = uriPatterns
                .CanvasPaintingAnnotationPage(manifestIdentifier, posterAssetIdentifier);
            manifest.PlaceholderCanvas = new Canvas
            {
                Id = uriPatterns.Canvas(manifestIdentifier, posterAssetIdentifier),
                Label = Lang.Map("Poster Image Canvas"),
                Width = 600, // we know this...
                Height = 400, // but not this. TODO: better poster images once in Goobi
                Items = new List<AnnotationPage>
                {
                    new AnnotationPage
                    {
                        Id = posterCanvasId,
                        Items = new List<IAnnotation>
                        {
                            new PaintingAnnotation
                            {
                                Id = uriPatterns.CanvasPaintingAnnotation(manifestIdentifier,
                                    posterAssetIdentifier),
                                Body = new Image
                                {
                                    Id = uriPatterns.PosterImage(manifestIdentifier),
                                    Label = Lang.Map("Poster Image"),
                                    Format = "image/jpeg"
                                },
                                Target = new Canvas { Id = posterCanvasId }
                            }
                        }
                    }
                }
            };
        }

        public void ImprovePagingSequence(Manifest manifest)
        {
            manifestStructureHelper.ImprovePagingSequence(manifest);
        }


        private (Image MainImage, Image ThumbImage) GetCanvasImages(IPhysicalFile physicalFile)
        {
            var assetIdentifier = physicalFile.StorageIdentifier;
            var imageService = uriPatterns.DlcsImageService(dlcsEntryPoint, assetIdentifier);
            var thumbService = uriPatterns.DlcsThumb(dlcsEntryPoint, assetIdentifier);
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

        private PaintingChoice GetAVChoice(IManifestation metsManifestation, IPhysicalFile physicalFile, Size videoSize, double duration)
        {
            // TODO - this needs work later. For now, we're deducing the properties of the output
            // based on inside knowledge of the Elastictranscoder settings.
            // Ideally, we ask the DLCS what it actually produced - including variations in size and duration 
            // The duration might not be identical.
            // The other issue here is that the DLCS probably won't have got round to processing this,
            // most times we get here. You'd have to come back and run the workflow again to pick it up.
            var choice = new PaintingChoice { Items = new List<IPaintable>() };
            if (physicalFile.Type == "Video" || metsManifestation.Type == "Video")
            {
                var confineToBox = new Size(1280, 720);
                // TODO - this needs to match Elastictranscoder settings, which may be more complex tham this
                var computedSize = Size.Confine(confineToBox, videoSize);
                choice.Items.Add(new Video
                {
                    Id = uriPatterns.DlcsVideo(dlcsEntryPoint, physicalFile.StorageIdentifier, "mp4"),
                    Format = "video/mp4",
                    Label = Lang.Map("MP4"),
                    Duration = duration,
                    Width = computedSize.Width,
                    Height = computedSize.Height
                });
                choice.Items.Add(new Video
                {
                    Id = uriPatterns.DlcsVideo(dlcsEntryPoint, physicalFile.StorageIdentifier, "webm"),
                    Format = "video/webm",
                    Label = Lang.Map("WebM"),
                    Duration = duration,
                    Width = computedSize.Width,
                    Height = computedSize.Height
                });
            }
            else if (physicalFile.Type == "Audio" || metsManifestation.Type == "Audio")
            {
                choice.Items.Add(new Audio
                {
                    Id = uriPatterns.DlcsAudio(dlcsEntryPoint, physicalFile.StorageIdentifier, "mp3"),
                    Format = "audio/mp3",
                    Label = Lang.Map("MP3"),
                    Duration = duration
                });
            }
            else
            {
                // ?
            }
            return choice;
        }
        
        
        private void AddAuthServices(
            IPaintable media, 
            IPhysicalFile physicalFile,
            Dictionary<string, IService> foundAuthServices)
        {
            switch (physicalFile.AccessCondition)
            {
                case AccessCondition.Open:
                    // no auth services needed, we're open and happy.
                    return;
                case AccessCondition.RequiresRegistration: // i.e., Clickthrough
                case AccessCondition.OpenWithAdvisory:     // also Clickthrough
                    AddAuthServiceToDictionary(foundAuthServices, clickthroughService);
                    AddAuthServiceToMedia(media, clickthroughServiceReference);
                    break;
                case AccessCondition.ClinicalImages: // i.e., Login (IIIF standard auth)
                case AccessCondition.Degraded:
                    AddAuthServiceToDictionary(foundAuthServices, loginService);
                    AddAuthServiceToMedia(media, loginServiceReference);
                    break;
                case AccessCondition.RestrictedFiles: // i.e., IIIF external auth
                    AddAuthServiceToDictionary(foundAuthServices, externalAuthService);
                    AddAuthServiceToMedia(media, externalAuthServiceReference);
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
        
        private void AddAuthServiceToMedia(IPaintable? paintable, IService? service)
        {
            if (paintable == null || service == null)
            {
                return;
            }

            switch (paintable)
            {
                case Image image:
                    var iiifImageApi2 = (ImageService2) image.Service.First();
                    iiifImageApi2.Service = new List<IService>{service};
                    break;
                case PaintingChoice choice:
                    foreach (var item in choice.Items ?? Enumerable.Empty<IPaintable>())
                    {
                        item.Service ??= new List<IService>();
                        item.Service.Add(service);
                    }
                    break;
                default:
                    paintable.Service ??= new List<IService>();
                    paintable.Service.Add(service);
                    break;
            }
        }

        public void Structures(Manifest manifest, IManifestation metsManifestation)
        {
            var physIdDict = metsManifestation.Sequence.ToDictionary(
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

        public void ManifestLevelAnnotations(Manifest manifest, IManifestation metsManifestation, bool addAllContentAnnos)
        {
            if (metsManifestation.Sequence.SupportsSearch())
            {
                manifest.Annotations = new List<AnnotationPage>
                {
                    new()
                    {
                        Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(metsManifestation.Id, 3),
                        Label = Lang.Map($"OCR-identified images and figures for {metsManifestation.Id}")
                    }
                };
                if (addAllContentAnnos)
                {
                    manifest.Annotations.Add(new()
                    {
                        Id = uriPatterns.ManifestAnnotationPageAllWithVersion(metsManifestation.Id, 3),
                        Label = Lang.Map($"All OCR-derived annotations for {metsManifestation.Id}")
                    });
                }
            }
        }

        public void Metadata(ResourceBase iiifResource, Work work)
        {
            var builder = new MetadataBuilder(work);
            iiifResource.Metadata = builder.Metadata;
        }

        /// <summary>
        /// Where am I in the tree?
        /// </summary>
        /// <param name="iiifResource"></param>
        /// <param name="work"></param>
        /// <param name="childManifestationsSource"></param>
        public void ArchiveCollectionStructure(ResourceBase iiifResource, Work work,
            Func<List<Manifestation>> childManifestationsSource)
        {
            // We don't know if the children of this item (work.Parts) are
            // manifests or collections. We could do this by loading them from the
            // catalogue API and seeing if they have a digital location, but that
            // would be expensive. Instead we look at known sibling manifestations in the
            // DDS Database. This means this will give more accurate results as the DB
            // fills up - it requires a full DB to be accurate.
            // This is OK because we should only get here in runtime, navigating DOWN
            // the archival hierarchy.
            if (work.Parts.Any() && iiifResource is Collection collection)
            {
                collection.Items = new List<ICollectionItem>();
                var knownChildManifestations = childManifestationsSource();
                foreach (var part in work.Parts)
                {
                    var manifestationForPart =
                        knownChildManifestations.SingleOrDefault(m => m.ReferenceNumber == part.ReferenceNumber);
                    var iiifPart = MakePart(part, manifestationForPart);
                    if (iiifPart != null)
                    {
                        collection.Items.Add(iiifPart);
                    }
                }
                collection.Label!["en"].Add(
                    $"({collection.Items.Count} of {work.Parts.Length} child parts are digitised works)");
            }

            var currentWork = work;
            var currentIiifResource = iiifResource;
            while (currentWork != null)
            {
                var parentWork = currentWork.PartOf?.LastOrDefault();
                if (parentWork != null)
                {
                    if (MakePart(parentWork, null) is Collection parentCollection)
                    {
                        currentIiifResource.PartOf ??= new List<ResourceBase>();
                        currentIiifResource.PartOf.Insert(0, parentCollection);
                        currentIiifResource = parentCollection;
                    }
                }
                currentWork = parentWork;
            }
        }

        private ICollectionItem? MakePart(Work work, Manifestation? manifestation)
        {
            if (manifestation != null || work.HasIIIFDigitalLocation())
            {
                // definitely a manifest
                return new Manifest
                {
                    Id = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber),
                    Label = Lang.Map(work.Title),
                    Thumbnail = manifestation?.GetThumbnail()
                };
            }
            // maybe a manifest, if only it were digitised... but, maybe just an undigitised work, or child structure.
            if (work.TotalParts > 0)
            {
                return new Collection
                {
                    Id = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber),
                    Label = Lang.Map(work.Title)
                };
            }

            return null;
        }

        public void CheckForCopyAndVolumeStructure(
            IManifestation metsManifestation,
            State state)
        {
            if (metsManifestation.ModsData.CopyNumber > 0)
            {
                if (state == null)
                {
                    // TODO - don't control application flow through exceptions, come back to this
                    throw new IIIFBuildStateException(
                        $"State is required to build {metsManifestation.Id}");
                }
                state.MultiCopyState ??= new MultiCopyState();
                state.MultiCopyState.CopyAndVolumes[metsManifestation.Id] = new CopyAndVolume
                {
                    Id = metsManifestation.Id,
                    CopyNumber = metsManifestation.ModsData.CopyNumber,
                    VolumeNumber = metsManifestation.ModsData.VolumeNumber
                };
            }
        }

        public void ProcessAVState(MultipleBuildResult buildResults, State state)
        {
            // OK, what do we have here?
            if (buildResults.Count <= 1)
            {
                // we only ended up with one manifest, so we're probably OK here.
                // If there are none, let the consequences of that be felt elsewhere.
                return;
            } 
            // get the BuildResult that has a video or audio canvas

            var relevantBuildResults = buildResults
                .Where(br => br.IIIFResource is Manifest)
                .Where(br =>
                    ((Manifest) br.IIIFResource).Items.HasItems() &&
                    ((Manifest) br.IIIFResource).Items.Exists(c => c.Duration > 0));

            
            string newId = buildResults.Identifier;
            var avCanvases = new List<Canvas>();
            BuildResult firstManifestationBuildResult = null;
            foreach (var relevantBuildResult in relevantBuildResults)
            {
                var manifest = (Manifest) relevantBuildResult.IIIFResource;
                if (firstManifestationBuildResult == null)
                {
                    firstManifestationBuildResult = relevantBuildResult;
                }
                var canvases = manifest.Items.Where(c => c.Duration > 0);
                // we now have the right Manifest, but it has the wrong Identifiers everywhere...
                string oldId = relevantBuildResult.Id;
                relevantBuildResult.Id = newId;
                manifest.Id = manifest.Id.Replace(oldId, newId);
                if (manifest.PartOf.HasItems())
                {
                    // This is no longer part of a collection
                    manifest.PartOf.RemoveAll(po => po.IsMultiPart());
                }
                foreach (var canvas in canvases)
                {
                    ChangeCanvasIds(canvas, oldId, newId, false);
                    avCanvases.Add(canvas);
                }
                ChangeCanvasIds(manifest.PlaceholderCanvas, oldId, newId, true);
            }
            if (state.FileState != null && state.FileState.FoundFiles.HasItems())
            {
                var transcripts = state.FileState.FoundFiles.Where(pf => pf.Type == "Transcript").ToList();
                if (!transcripts.HasItems()) transcripts = state.FileState.FoundFiles.ToList();
                // allocate the transcripts to the canvases.
                // This is not a very clever way of doing it but almost certainly won't be a problem, any complex
                // AV with multiple canvases and transcripts will come through the new workflow.
                for (int tCounter = 0; tCounter < transcripts.Count; tCounter++)
                {
                    if (tCounter < avCanvases.Count)
                    {
                        AddSupplementingPdfToCanvas(buildResults.Identifier, avCanvases[tCounter], transcripts[tCounter].Files[0], "transcript", "PDF Transcript");
                    }
                }
            }
            // Now, discard the other buildResults
            buildResults.RemoveAll();
            if (firstManifestationBuildResult != null)
            {
                buildResults.Add(firstManifestationBuildResult);
                if (firstManifestationBuildResult.IIIFResource is Manifest firstManifest)
                {
                    firstManifest.Items = avCanvases;
                }
            }
        }

        private void AddSupplementingPdfToCanvas(string manifestIdentifier, Canvas canvas, IStoredFile pdfFile,
            string annoIdentifier, string label)
        {
            var pageCountMetadata = GetPageCountMetadata(pdfFile.PhysicalFile);
            List<LabelValuePair>? resourceMetadata = null;
            if (pageCountMetadata != null)
            {
                resourceMetadata = new List<LabelValuePair> {pageCountMetadata};
            }
            canvas.Annotations ??= new List<AnnotationPage>();
            canvas.Annotations.Add(new AnnotationPage
            {
                Id = uriPatterns.CanvasSupplementingAnnotationPage(
                    manifestIdentifier, pdfFile.StorageIdentifier),
                Items = new List<IAnnotation>
                {
                    new SupplementingDocumentAnnotation
                    {
                        Id = uriPatterns.CanvasSupplementingAnnotation(
                            manifestIdentifier, pdfFile.StorageIdentifier, annoIdentifier),
                        Body = new ExternalResource("Text")
                        {
                            Id = uriPatterns.DlcsFile(dlcsEntryPoint, pdfFile.StorageIdentifier),
                            Label = Lang.Map(label),
                            Format = "application/pdf",
                            Metadata = resourceMetadata
                        },
                        Target = new Canvas {Id = canvas.Id}
                    }
                }
            });
        }

        private static void ChangeCanvasIds(Canvas? canvas, string oldId, string newId, bool changeImageBody)
        {
            if (canvas == null)
            {
                return;
            }
            string oldIdPath = $"/{oldId}/";
            string newIdPath = $"/{newId}/";
            canvas.Id = canvas.Id.Replace(oldIdPath, newIdPath);
            canvas.Items[0].Id = canvas.Items[0].Id.Replace(oldIdPath, newIdPath);
            var anno = (PaintingAnnotation) canvas.Items[0].Items[0];
            anno.Id = anno.Id.Replace(oldIdPath, newIdPath);
            var target = anno.Target as ResourceBase;
            if (target != null)
            {
                target.Id = target.Id.Replace(oldIdPath, newIdPath);
            }

            if (!changeImageBody) return;
            if (anno.Body is Image image)
            {
                image.Id = image.Id.Replace(oldId, newId);
            }
        }

        public void AddTrackingLabel(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var mdFormat = manifestationMetadata.Manifestations.FirstOrDefault()?.RootSectionType;
            var format = mdFormat.HasText() ? mdFormat : "n/a";

            var partner = PartnerAgents.GetPartner(manifestationMetadata.Metadata.GetLocationOfOriginal());
            var institution = partner != null ? partner.Label : "n/a";

            var mdDigicode = manifestationMetadata.Metadata.GetDigitalCollectionCodes().FirstOrDefault();
            var digicode = mdDigicode.HasText() ? mdDigicode : "n/a";
            
            var mdCalmRef = manifestationMetadata.Manifestations.FirstOrDefault()?.ReferenceNumber;
            var collectioncode = mdCalmRef.HasText() ? mdCalmRef : "n/a";

            string trackingLabel = "Format: " + format +
                        ", Institution: " + institution +
                        ", Identifier: " + manifestationMetadata.Identifier.BNumber +
                        ", Digicode: " + digicode +
                        ", Collection code: " + collectioncode;


            ((ICollectionItem)iiifResource).Services ??= new List<IService>();
            ((ICollectionItem)iiifResource).Services?.Add(
                new ExternalResource("Text")
                {
                    Id = iiifResource.Id + "#tracking",
                    Profile = Constants.Profiles.TrackingExtension,
                    Label = Lang.Map(trackingLabel)
                });
        }

        public void AddTimestampService(ResourceBase iiifResource)
        {            
            ((ICollectionItem)iiifResource).Services ??= new List<IService>();
            ((ICollectionItem)iiifResource).Services?.Add(
                new ExternalResource("Text")
                {
                    Id = iiifResource.Id + "#timestamp",
                    Profile = Constants.Profiles.BuilderTime,
                    Label = Lang.Map("none", DateTime.UtcNow.ToString("O"))
                });
        }

        public void AddAccessHint(Manifest manifest, IManifestation metsManifestation, string identifier)
        {
            var accessConditions = metsManifestation.Sequence
                .Select(pf => pf.AccessCondition);
            var mostSecureAccessCondition = AccessCondition.GetMostSecureAccessCondition(accessConditions);
            string accessHint;
            switch (mostSecureAccessCondition)
            {
                case null:
                case AccessCondition.Open:
                    accessHint = "open";
                    break;
                case AccessCondition.RequiresRegistration:
                case AccessCondition.OpenWithAdvisory:
                    accessHint = "clickthrough";
                    break;
                default:
                    accessHint = "credentials";
                    break;
            }            
            manifest.Services ??= new List<IService>();
            manifest.Services?.Add(
                new ExternalResource("Text")
                {
                    Id = manifest.Id + "#accesscontrolhints",
                    Profile = Constants.Profiles.AccessControlHints,
                    Label = Lang.Map(accessHint)
                });

        }
    }
}