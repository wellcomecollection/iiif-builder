using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.Auth.V2;
using IIIF.ImageApi.V2;
using IIIF.Presentation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V1;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.AuthServices;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig;
using Wellcome.Dds.Repositories.Presentation.SpecialState;
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
        private readonly string[] extraAccessConditions;
        private readonly ManifestStructureHelper manifestStructureHelper;
        private readonly IDigitalObjectRepository digitalObjectRepository;

        // Existing Auth1 (actually 0.9.3) services
        private readonly IService clickthroughServiceV1;
        private readonly IService clickthroughServiceReferenceV1;
        private readonly IService loginServiceV1;
        private readonly IService loginServiceReferenceV1;
        private readonly IService externalAuthServiceV1;
        private readonly IService externalAuthServiceReferenceV1;
        
        // New Auth2 (provisional) services
        private readonly IService clickthroughServiceV2;
        private readonly IService clickthroughServiceReferenceV2;
        private readonly IService loginServiceV2;
        private readonly IService loginServiceReferenceV2;
        private readonly IService externalAuthServiceV2;
        private readonly IService externalAuthServiceReferenceV2;

        private readonly IAuthServiceProvider authServiceProvider;
        
        // omit Digitalcollection and Location
        private static readonly string[] DisplayedAggregations = {"Genre", "Subject", "Contributor"};
        
        // Two behavior values for born digital
        private static readonly List<string> OriginalBehavior = new() { "original" };
        private static readonly List<string> PlaceholderBehavior = new() { "placeholder" };
        private static readonly Size PlaceholderCanvasSize = new Size(1000, 800);
        private static readonly Size PlaceholderThumbnailSize = new Size(101, 151);
        
        public IIIFBuilderParts(
            IDigitalObjectRepository digitalObjectRepository,
            UriPatterns uriPatterns,
            string dlcsEntryPoint,
            bool referenceV0SearchService, 
            string[] extraAccessConditions)
        {
            this.uriPatterns = uriPatterns;
            this.dlcsEntryPoint = dlcsEntryPoint;
            this.referenceV0SearchService = referenceV0SearchService;
            this.extraAccessConditions = extraAccessConditions;
            this.digitalObjectRepository = digitalObjectRepository;
            manifestStructureHelper = new ManifestStructureHelper();
            authServiceProvider = new IIIFAuthServiceProvider(dlcsEntryPoint, uriPatterns);
            clickthroughServiceV1 = authServiceProvider.GetAcceptTermsAuthServicesV1();
            clickthroughServiceReferenceV1 = new V2ServiceReference(clickthroughServiceV1);
            loginServiceV1 = authServiceProvider.GetClinicalLoginServicesV1();
            loginServiceReferenceV1 = new V2ServiceReference(loginServiceV1);
            externalAuthServiceV1 = authServiceProvider.GetRestrictedLoginServicesV1();
            externalAuthServiceReferenceV1 = new V2ServiceReference(externalAuthServiceV1);

            clickthroughServiceV2 = authServiceProvider.GetAcceptTermsAuthServicesV2();
            clickthroughServiceReferenceV2 = new AuthAccessService2 { Id = clickthroughServiceV2.Id };
            loginServiceV2 = authServiceProvider.GetLoginServicesV2();
            loginServiceReferenceV2 = new AuthAccessService2 { Id = loginServiceV2.Id };
            externalAuthServiceV2 = authServiceProvider.GetRestrictedLoginServicesV2();
            externalAuthServiceReferenceV2 = new AuthAccessService2 { Id = externalAuthServiceV2.Id };
        }


        public void HomePage(ResourceBase iiifResource, Work work)
        {
            iiifResource.Homepage = new List<ExternalResource>
            {
                new("Text")
                {
                    Id = uriPatterns.PersistentPlayerUri(work.Id!),
                    Label = Lang.Map(work.Title!),
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
                        // don't use Location or DigitalCollection as membership aggregations
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
                    Id = uriPatterns.CatalogueApi(work.Id!),
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
            var usage = LicenceHelpers.GetUsageWithHtmlLinks(metsManifestation.SectionMetadata!.Usage);
            if (!usage.HasText())
            {
                var code = GetMappedLicenceCode(metsManifestation);
                if (code.HasText())
                {
                    var dict = PlayerConfigProvider.BaseConfig.Modules.ConditionsDialogue.Content;
                    usage = dict.ContainsKey(code) ? dict[code] : null;
                }
            }
            if (usage != null && !usage.StartsWith("<"))
            {
                // Make HTML
                usage = usage.WrapSpan();
            }

            // NOTE - the PDF uses the first string that is not "Wellcome Collection" for output
            var attribution = Constants.WellcomeCollection;
            var locationOfOriginal = manifestationMetadata.Metadata.GetLocationOfOriginal();
            if (locationOfOriginal.HasText())
            {
                attribution = locationOfOriginal;
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
                    manifest.AddMetadataPair(attributionAndUsage);
                }
            }
            // TODO - what do we want to do with this?
            // Park for now and resurrect later, it all depends on what the wc.org front end wants to do with these things
            // var permittedOps = digitisedManifestation.MetsManifestation.PermittedOperations;
            // var accessCondition = digitisedManifestation.MetsManifestation.SectionData.AccessCondition;
        }

        public void Rights(Manifest manifest, IManifestation metsManifestation)
        {
            var code = GetMappedLicenceCode(metsManifestation);
            if (!code.HasText()) return;
            var uri = LicenseMap.GetLicenseUri(code);
            if (!uri.HasText()) return;
            
            // the machine-readable versions use http IDs 
            uri = uri.Replace("https://creativecommons.org/", "http://creativecommons.org/");
            uri = uri.Replace("https://rightsstatements.org/", "http://rightsstatements.org/");
            manifest.Rights = uri;
        }

        private static string? GetMappedLicenceCode(IManifestation metsManifestation)
        {
            var dzl = metsManifestation.SectionMetadata!.DzLicenseCode;
            return LicenceCodes.MapLicenseCode(dzl);
        }

        public void PagedBehavior(Manifest manifest, IManifestation metsManifestation)
        {
            var structType = metsManifestation.RootStructRange!.Type;
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
                    Id = uriPatterns.DlcsPdf(dlcsEntryPoint, metsManifestation.Identifier!),
                    Label = Lang.Map("View as PDF"),
                    Format = "application/pdf"
                });
            }

            if (metsManifestation.Sequence!.SupportsSearch())
            {
                manifest.Rendering ??= new List<ExternalResource>();
                manifest.Rendering.Add(new ExternalResource("Text")
                {
                    Id = uriPatterns.RawText(metsManifestation.Identifier!),
                    Label = Lang.Map("View raw text"),
                    Format = "text/plain"
                });
            }
        }

        public void SearchServices(Manifest manifest, IManifestation metsManifestation)
        {
            if (metsManifestation.Sequence!.SupportsSearch())
            {
                manifest.EnsureContext(SearchService.Search1Context);
                manifest.Service ??= new List<IService>();
                var searchServiceId = referenceV0SearchService ? 
                    uriPatterns.IIIFContentSearchService0(metsManifestation.Identifier!) : 
                    uriPatterns.IIIFContentSearchService1(metsManifestation.Identifier!);
                manifest.Service.Add(new SearchService
                {
                    Id = searchServiceId,
                    Profile = SearchService.Search1Profile,
                    Label = new MetaDataValue("Search within this manifest"),
                    Service = new AutoCompleteService
                    {
                        Id = uriPatterns.IIIFAutoCompleteService1(metsManifestation.Identifier!),
                        Profile = AutoCompleteService.AutoCompleteService1Profile,
                        Label = new MetaDataValue("Autocomplete words in this manifest")
                    }
                });
            }
        }
        
        public void Canvases(Manifest manifest, IManifestation metsManifestation, State? state, BuildResult buildResult)
        {
            var isBornDigitalManifestation = metsManifestation.Type == "Born Digital"; // define as const - but where?
            var foundAuthServices = new Dictionary<string, IService>();
            var manifestIdentifier = metsManifestation.Identifier.ToString();
            manifest.Items = new List<Canvas>();
            var canvasesWithNewWorkflowTranscripts = new List<Canvas>();
            foreach (var physicalFile in metsManifestation.Sequence!)
            {
                LanguageMap canvasLabel;
                if (isBornDigitalManifestation) 
                {
                    canvasLabel = Lang.Map("none", physicalFile.OriginalName!.GetFileName()!);
                }
                else if (physicalFile.OrderLabel.HasText())
                {
                    canvasLabel = Lang.Map("none", physicalFile.OrderLabel);
                }
                else
                {
                    canvasLabel = Lang.Map("none", physicalFile.Index.ToString());
                }
                var canvas = new Canvas
                {
                    Id = uriPatterns.Canvas(manifestIdentifier, physicalFile.StorageIdentifier),
                    Label = canvasLabel
                };
                manifest.Items.Add(canvas);

                bool isForIIIFManifest = AccessCondition.IsForIIIFManifest(physicalFile.AccessCondition!);
                bool includeBecauseExtraConfig = extraAccessConditions.Contains(physicalFile.AccessCondition);

                if (!(isForIIIFManifest || includeBecauseExtraConfig))
                {
                    // has an unknown or forbidden access condition, and config hasn't overridden this
                    canvas.Label["en"] = new List<string>{$"Excluded access condition: {physicalFile.AccessCondition}"};
                    canvas.Summary = Lang.Map("This asset is not currently available online");
                    continue;
                }

                if (includeBecauseExtraConfig)
                {
                    canvas.Label["en"] = new List<string>{$"WARNING: Access Condition {physicalFile.AccessCondition}"};
                }
                
                var assetIdentifier = physicalFile.StorageIdentifier;
                
                switch (physicalFile.Family)
                {
                    case AssetFamily.Image:
                        buildResult.ImageCount += 1;
                        var size = physicalFile.GetWhSize();
                        if (size == null)
                        {
                            throw new NotSupportedException("No image dimensions for " + assetIdentifier);
                        }
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
                                    Label = Lang.Map(physicalFile.OrderLabel.HasText() ? $"Text of page {physicalFile.OrderLabel}" : "Text of this page")
                                }
                            };
                            canvas.Rendering ??= new List<ExternalResource>();
                            canvas.Rendering.Add(new("Image")
                            {
                                Id = uriPatterns.SvgPage(manifestIdentifier, assetIdentifier),
                                Format = "image/svg+xml",
                                Label = new LanguageMap("en", "SVG XML for page text")
                            });
                        }

                        AddAuthServices(mainImage, physicalFile, foundAuthServices);

                        var deliveredFiles = digitalObjectRepository.GetDeliveredFiles(physicalFile);
                        var asFile = deliveredFiles.SingleOrDefault(df => df.DeliveryChannel == Channels.File);
                        if (asFile != null)
                        {
                            var label = physicalFile.MimeType == "image/jp2" ? "JPEG 2000" : physicalFile.MimeType;
                            var rendering = new Image
                            {
                                Id = asFile.DlcsUrl,
                                Format = physicalFile.MimeType,
                                Label = Lang.Map(label ?? "(unknown format)")
                            };
                            canvas.Rendering ??= new List<ExternalResource>();
                            canvas.Rendering.Add(rendering);
                            AddAuthServices(rendering, physicalFile, foundAuthServices);
                        }
                        break;
                    
                    case AssetFamily.TimeBased:
                        buildResult.TimeBasedCount += 1;
                        if (!isBornDigitalManifestation && state == null)
                        {
                            // We need this state to build OLD workflow videos, because we need to consider
                            // other manifestations within a multiple manifestation.
                            
                            // But we don't need this state to build a new workflow video, which all lives
                            // in a single manifestation.
                            // https://github.com/wellcomecollection/platform/issues/4788
                            
                            // So how do we know what kind of workflow this is?
                            // Does this IPhysicalFile have an explicit USE="ACCESS" file group?
                            // For now we will assume that this is a sign of the new workflow
                            var accessFile = physicalFile.Files!.FirstOrDefault(f => f.Use == "ACCESS");
                            if(accessFile == null)
                            {
                                throw new IIIFBuildStateException(
                                    "State is required to build AV resources for OLD WORKFLOWS");
                            }
                        }
                        Size? videoSize = physicalFile.GetWhSize();
                        if (isBornDigitalManifestation && physicalFile.MimeType.IsVideoMimeType())
                        {
                            // Missing size is an error for BD, but not (yet) for Goobi
                            if (videoSize == null)
                            {
                                throw new NotSupportedException("No video w,h dimensions for " + assetIdentifier);
                            }
                        }
                        
                        double duration = physicalFile.AssetMetadata!.GetDuration();
                        if (isBornDigitalManifestation && 
                            (physicalFile.MimeType.IsVideoMimeType() || physicalFile.MimeType.IsAudioMimeType()))
                        {
                            // Missing duration is an error for BD, but not (yet) for Goobi
                            if (duration <= 0)
                            {
                                throw new NotSupportedException("No duration for " + assetIdentifier);
                            }
                        }

                        // Without more support in Goobi we might have ended up with 0,0,0 here.
                        // So we'll fake some obvious sizes

                        if (physicalFile.Type == "Video" || metsManifestation.Type == "Video")
                        {
                            // These types are from Goobi workflow
                            // Assign a fake size if we haven't got one
                            videoSize ??= new Size(999, 999);
                        }
                            
                        if (videoSize != null)
                        {
                            canvas.Width = videoSize.Width;
                            canvas.Height = videoSize.Height;
                        }
                        if (duration <= 0)
                        {
                            duration = 999.99;
                        }
                        canvas.Duration = duration;
                        var avChoice = GetAVChoice(physicalFile, videoSize, duration);
                        if (avChoice.Items?.Count > 0)
                        {
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
                                            Body = avChoice.Items.Count == 1 ? avChoice.Items[0] : avChoice,
                                            Target = new Canvas {Id = canvas.Id}
                                        }
                                    }
                                }
                            };
                            
                            // Also add the same videos as renderings for download:
                            canvas.Rendering ??= new List<ExternalResource>();
                            
                            // Now add auth services, and add them to the rendering
                            foreach (var paintable in avChoice.Items)
                            {
                                if (paintable is ResourceBase resource)
                                {
                                    AddAuthServices(resource, physicalFile, foundAuthServices);
                                    canvas.Rendering.Add((ExternalResource)resource);
                                }
                            }

                            if (!isBornDigitalManifestation)
                            {
                                // This is still the DDS-hosted poster image, for old and new AV workflows
                                AddPosterImage(manifest, assetIdentifier, manifestIdentifier);
                                var transcriptPdf = physicalFile.Files!.FirstOrDefault(f => f.Use == "TRANSCRIPT");
                                if (transcriptPdf != null)
                                {
                                    // A new workflow transcript for this AV file
                                    AddSupplementingPdfToCanvas(manifestIdentifier, canvas, transcriptPdf, "transcript", "PDF Transcript");
                                    canvasesWithNewWorkflowTranscripts.Add(canvas);
                                }
                            }
                        }

                        if (!isBornDigitalManifestation)
                        {
                            var betterCanvasLabel = GetBetterAVCanvasLabel(metsManifestation, physicalFile);
                            if (betterCanvasLabel != null)
                            {
                                canvas.Label = Lang.Map(betterCanvasLabel);
                            }
                        }

                        if (state != null)
                        {
                            state.AVState ??= new AVState();
                            state.AVState.Canvases.Add(canvas); // is this enough? Map them, somehow?
                        }
                        break;
                        
                    case AssetFamily.File:
                        buildResult.FileCount += 1;
                        if (isBornDigitalManifestation || metsManifestation.Type == "Monograph")
                        {
                            // We need our born-digital extensions
                            manifest.EnsureContext(uriPatterns.BornDigitalExtensionContext());
                            
                            // TODO: is this simple logic OK for every BD PDF? See what comparison tool reveals.
                            // This is a born digital file that is not image or AV, and is not
                            // the transcript manifestation for a Goobi AV file.
                            
                            var bornDigitalFile = physicalFile.Files!.FirstOrDefault();
                            if (bornDigitalFile != null)
                            {
                                canvas.AddNonLangMetadata("File format", physicalFile.AssetMetadata!.GetFormatName());
                                canvas.AddNonLangMetadata("File size", StringUtils.FormatFileSize(physicalFile.AssetMetadata.GetFileSize(), true));
                                canvas.AddNonLangMetadata("Pronom key", physicalFile.AssetMetadata.GetPronomKey());
                                // AddSupplementingPdfToCanvas(manifestIdentifier, canvas, bornDigitalPdf, 
                                //    "pdf", manifest.Label.ToString());

                                var externalResource = new ExternalResource(GetDcType(physicalFile))
                                {
                                    Id = uriPatterns.DlcsFile(dlcsEntryPoint, bornDigitalFile.StorageIdentifier),
                                    Format = physicalFile.MimeType,
                                    Behavior = OriginalBehavior,
                                    Label = canvas.Label
                                };
                                AddAuthServices(externalResource, physicalFile, foundAuthServices);
                                canvas.Rendering ??= new List<ExternalResource>();
                                canvas.Rendering.Add(externalResource);

                                canvas.Behavior = PlaceholderBehavior;
                                AddBornDigitalCanvasPlaceholderImage(canvas, physicalFile, manifestIdentifier, assetIdentifier);
                                var pageCountMetadata = GetPageCountMetadata(physicalFile);
                                canvas.AddMetadataPair(pageCountMetadata);
                                
                                // TODO - other file characteristics
                                
                                if (metsManifestation.Type == "Monograph")
                                {
                                    // The previous model added some info to the Manifest; for Goobi PDFs,
                                    // there will only be one file in the Manifest.
                                    manifest.AddMetadataPair(pageCountMetadata);
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
                            
                        }
                        else
                        {
                            // An AV transcript. Note it down and add it to the AVState
                            state!.FileState ??= new FileState();
                            state.FileState.FoundFiles.Add(physicalFile);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (isBornDigitalManifestation)
                {
                    var originalName = physicalFile.AssetMetadata!.GetOriginalName();
                    canvas.AddNonLangMetadata("Full path", originalName);
                    var possibleNavDate = physicalFile.AssetMetadata.GetCreatedDate();
                    if (possibleNavDate.HasValue)
                    {
                        canvas.NavDateDateTime = possibleNavDate;
                    }
                }
            }
            
            MoveSingleAVTranscriptToManifest(manifest, canvasesWithNewWorkflowTranscripts, true);

            if (foundAuthServices.HasItems())
            {
                manifest.Services ??= new List<IService>();
                manifest.Services.AddRange(foundAuthServices.Values);
                // A point release of Presentation 3 could add Auth2 services to the context
                manifest.EnsureContext(IIIF.Auth.V2.Constants.IIIFAuth2Context);
            }
        }

        /// <summary>
        /// This is a "stock" image that acts as a placeholder for the born digital file that's
        /// associated with the canvas via rendering. For now, it's the same image every time.
        /// But later we could produce an image representation of the file.
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="physicalFile">Not yet used, but custom placeholder images would be based on this</param>
        /// <param name="manifestIdentifier"></param>
        /// <param name="assetIdentifier"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void AddBornDigitalCanvasPlaceholderImage(
            Canvas canvas, IPhysicalFile physicalFile, 
            string manifestIdentifier, string? assetIdentifier)
        {
            var pronomKey = physicalFile.AssetMetadata!.GetPronomKey();
            if (pronomKey.IsNullOrEmpty())
            {
                pronomKey = "fmt/0";
            }
            canvas.Width = PlaceholderCanvasSize.Width;
            canvas.Height = PlaceholderCanvasSize.Height;                        
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
                            Body = new Image
                            {
                                Id = uriPatterns.CanvasFilePlaceholderImage(
                                    pronomKey, 
                                    physicalFile.MimeType),
                                Width = PlaceholderCanvasSize.Width,
                                Height = PlaceholderCanvasSize.Height,
                                Format = "image/png"
                            },
                            Target = new Canvas{ Id = canvas.Id }
                        }
                    }
                }
            };
            canvas.Thumbnail = new List<ExternalResource>
            {
                new Image
                {
                    Id = uriPatterns.CanvasFilePlaceholderThumbnail(
                        pronomKey, 
                        physicalFile.MimeType),
                    Width = PlaceholderThumbnailSize.Width,
                    Height = PlaceholderThumbnailSize.Height,
                    Format = "image/png"
                }
            };  
        }

        /// <summary>
        /// What Dublin Core type should be assigned to this born digital external resource?
        /// </summary>
        /// <param name="physicalFile"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static string GetDcType(IPhysicalFile physicalFile)
        {
            if (physicalFile.MimeType.IsTextMimeType())
            {
                return "Text";
            }
            if (physicalFile.MimeType.IsImageMimeType())
            {
                return "Image";
            }
            if (physicalFile.MimeType.IsVideoMimeType())
            {
                return "Video";
            }
            if (physicalFile.MimeType.IsAudioMimeType())
            {
                return "Audio";
            }

            // return "Dataset"; 
            return "Text"; // This is probably safest for now but we could have a more complex pronom lookup
        }

        private static void MoveSingleAVTranscriptToManifest(
            Manifest manifest,
            List<Canvas> canvasesWithNewWorkflowTranscripts,
            bool asRendering)
        {
            if (manifest.Items != null)
            {
                if ((asRendering || manifest.Items.Count > 1) && canvasesWithNewWorkflowTranscripts.Count == 1)
                {
                    // Wellcome AV-specific transcript behaviour. Move the canvas's PDF transcript to the manifest.
                    // If there is only one transcript and only one canvas then it STAYS ON THE CANVAS because that is
                    // more correct; it's only to avoid having a transcript with text from other canvases.
                    var canvas = canvasesWithNewWorkflowTranscripts[0];
                    var annoPage = canvas.Annotations?[0];
                    if (annoPage == null) return;

                    if (asRendering)
                    {
                        if (annoPage.Items?.OfType<SupplementingDocumentAnnotation>().FirstOrDefault()?.Body is ExternalResource pdf)
                        {
                            manifest.Rendering ??= new List<ExternalResource>();
                            manifest.Rendering.Add(pdf);
                        }
                    }
                    else
                    {
                        manifest.Annotations ??= new List<AnnotationPage>();
                        manifest.Annotations.Add(annoPage);
                    }
                    if (manifest.Items.Count > 1)
                    {
                        // we can only leave the Canvas one in place if it's the _only_ one.
                        canvas.Annotations!.RemoveAll(ap => ap.Id == annoPage.Id);
                        if (canvas.Annotations.Count == 0)
                        {
                            canvas.Annotations = null;
                        }
                    }
                }
                
            }
        }

        private static string? GetBetterAVCanvasLabel(IManifestation metsManifestation, IPhysicalFile physicalFile)
        {
            string? betterCanvasLabel = null;
            // Find a better Canvas label for an AV canvas by seeing if this file has one
            // single corresponding logical struct div. This is very conservative in deciding
            // what a "structural" logical struct div is. Later, we might want TOCs etc - but they 
            // can come from normal range-building behaviour.
            var logicalStructs = metsManifestation.RootStructRange?.Children;
            if (logicalStructs.HasItems())
            {
                var logicalStructsForFile = logicalStructs
                    .Where(s => s.PhysicalFileIds!.Contains(physicalFile.Id))
                    .ToList();
                if (logicalStructsForFile.Count == 1)
                {
                    betterCanvasLabel = GetBetterAVCanvasLabel(
                        logicalStructsForFile[0].Label,
                        logicalStructsForFile[0].Type);
                }
            }

            return betterCanvasLabel;
        }

        public static string? GetBetterAVCanvasLabel(string? structLabel, string? structType)
        {
            string? betterCanvasLabel = null;
            var label = structLabel ?? "";
            var type = structType ?? "";
            if (!label.Contains("side", StringComparison.InvariantCultureIgnoreCase) && type.ToLowerInvariant().StartsWith("side"))
            {
                var humanReadable = "Side " + type.Substring(4).Trim();
                if (label.HasText())
                {
                    label += ", ";
                }

                label += humanReadable;
            }
            if (label.HasText())
            {
                betterCanvasLabel = label;
            }
            return betterCanvasLabel;
        }

        private LabelValuePair? GetPageCountMetadata(IPhysicalFile physicalFile)
        {
            var pageCount = physicalFile.AssetMetadata!.GetNumberOfPages();
            if (pageCount > 0)
            {
                var label = Lang.Map("en", "Number of pages");
                var value = Lang.Map("none", pageCount.ToString());
                return new LabelValuePair(label, value);
            }

            return null;
        }

        private void AddPosterImage(Manifest manifest, string? assetIdentifier, string manifestIdentifier)
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


        private (Image MainImage, Image? ThumbImage) GetCanvasImages(IPhysicalFile physicalFile)
        {
            var assetIdentifier = physicalFile.StorageIdentifier;
            var imageService = uriPatterns.DlcsImageService(dlcsEntryPoint, assetIdentifier);
            var thumbService = uriPatterns.DlcsThumb(dlcsEntryPoint, assetIdentifier);
            var sizes = physicalFile.GetAvailableSizes();
            var actualSize = sizes.First();
            var thumbSizes = sizes.Skip(1).ToList();
            var staticSize = actualSize;
            Image? thumbImage = null;
            if (thumbSizes.Any())
            {
                staticSize = thumbSizes.First();
                thumbImage = thumbService.AsThumbnailWithService(thumbSizes);
            }
            var mainImage = imageService.AsImageWithService(actualSize, staticSize);
            return (mainImage, thumbImage);
        }

        private PaintingChoice GetAVChoice(IPhysicalFile physicalFile, Size? videoSize, double duration)
        {
            // TODO - this needs work later. For now, we're deducing the properties of the output
            // based on inside knowledge of the Elastic Transcoder settings.
            // Ideally, we ask the DLCS what it actually produced - including variations in size and duration 
            // The duration might not be identical.
            // The other issue here is that the DLCS probably won't have got round to processing this,
            // most times we get here. You'd have to come back and run the workflow again to pick it up.
            var choice = new PaintingChoice { Items = new List<IPaintable>() };
            var deliveredFiles = digitalObjectRepository.GetDeliveredFiles(physicalFile);

            if (deliveredFiles.HasItems())
            {
                // This is the new way, using the information from DeliveryChannels.
                // we want to list the <= 720p video first, regardless of whether it's the file delivery channel
                // but we don't want to know about the value 720! so we'll just list them by size
                foreach (var avFile in deliveredFiles.OrderBy(f => f.Height))
                {
                    if (avFile.MediaType.IsVideoMimeType())
                    {
                        choice.Items.Add(new Video
                        {
                            Id = avFile.PublicUrl,
                            Format = avFile.MediaType,
                            Duration = avFile.Duration,
                            Width = avFile.Width,
                            Height = avFile.Height,
                            Label = Lang.Map($"Video file, size: {avFile.Width} x {avFile.Height}")
                        });
                    } 
                    else if (avFile.MediaType.IsAudioMimeType())
                    {
                        choice.Items.Add(new Audio
                        {
                            Id = avFile.PublicUrl,
                            Format = avFile.MediaType,
                            Duration = avFile.Duration,
                            Label = Lang.Map($"Audio file, {avFile.Duration} s")
                        });
                    }
                }
            }
            else
            {
               
                
                // This is the old method, which we can delete as soon as we know we're good with DeliveryChannels
                if (physicalFile.MimeType.IsVideoMimeType() && videoSize != null)
                {
                    var confineToBox = new Size(1280, 720);
                    var computedSize = Size.Confine(confineToBox, videoSize);
                    choice.Items.Add(new Video
                    {
                        Id = uriPatterns.DlcsVideo(dlcsEntryPoint, physicalFile.StorageIdentifier, "mp4"),
                        Format = "video/mp4",
                        Duration = duration,
                        Width = computedSize.Width,
                        Height = computedSize.Height,
                        Label = Lang.Map($"Video file, size: {computedSize.Width} x {computedSize.Height}")
                    });
                    // We have removed the WebM transcode from this output, and need to remove it from the 
                    // DLCS ElasticTranscoder settings.
                }
                else if (physicalFile.MimeType.IsAudioMimeType())
                {
                    choice.Items.Add(new Audio
                    {
                        Id = uriPatterns.DlcsAudio(dlcsEntryPoint, physicalFile.StorageIdentifier, "mp3"),
                        Format = "audio/mp3",
                        Duration = duration,
                        Label = Lang.Map($"Audio file, {duration} s")
                    });
                }
            }

            return choice;
        }

        private void AddAuthServices(
            ResourceBase media, 
            IPhysicalFile physicalFile,
            Dictionary<string, IService> foundAuthServices)
        {
            switch (physicalFile.AccessCondition)
            {
                case AccessCondition.Open:
                    // no auth services needed, we're open and happy.
                    return;
                case AccessCondition.RequiresRegistration: // i.e., Clickthrough - which is interactive
                case AccessCondition.OpenWithAdvisory:     // also Clickthrough
                    AddAuthServiceToDictionary(foundAuthServices, clickthroughServiceV1);
                    AddAuthServiceToDictionary(foundAuthServices, clickthroughServiceV2);
                    AddAuthServiceToMedia(media, clickthroughServiceReferenceV1);
                    AddAuthServiceToMedia(media, clickthroughServiceReferenceV2, 
                        authServiceProvider.GetClickthroughProbeService(physicalFile.StorageIdentifier!));
                    break;
                case AccessCondition.ClinicalImages: // i.e., Login (IIIF interactive auth)
                case AccessCondition.Degraded:
                    AddAuthServiceToDictionary(foundAuthServices, loginServiceV1);
                    AddAuthServiceToDictionary(foundAuthServices, loginServiceV2);
                    AddAuthServiceToMedia(media, loginServiceReferenceV1);
                    AddAuthServiceToMedia(media, loginServiceReferenceV2,
                        authServiceProvider.GetLoginProbeService(physicalFile.StorageIdentifier!));
                    break;
                case AccessCondition.RestrictedFiles: // i.e., IIIF external auth
                    AddAuthServiceToDictionary(foundAuthServices, externalAuthServiceV1);
                    AddAuthServiceToDictionary(foundAuthServices, externalAuthServiceV2);
                    AddAuthServiceToMedia(media, externalAuthServiceReferenceV1);
                    AddAuthServiceToMedia(media, externalAuthServiceReferenceV2,
                        authServiceProvider.GetRestrictedProbeService(physicalFile.StorageIdentifier!));
                    break;
                default:
                    if (!extraAccessConditions.Contains(physicalFile.AccessCondition))
                    {
                        throw new NotImplementedException("Unknown access condition " + physicalFile.AccessCondition);
                    }
                    break;
            }
        }

        private void AddAuthServiceToDictionary(Dictionary<string, IService> foundAuthServices, IService service)
        {
            if (!foundAuthServices.ContainsKey(service.Id!))
            {
                foundAuthServices[service.Id!] = service;
            }
        }
        
        private void AddAuthServiceToMedia(ResourceBase? resource, IService? service, AuthProbeService2? parentProbe = null)
        {
            if (resource == null || service == null)
            {
                return;
            }

            var serviceToAdd = service;
            if (parentProbe != null)
            {
                serviceToAdd = parentProbe;
                parentProbe.Service ??= new List<IService>();
                parentProbe.Service.Add(service);
            }
            switch (resource)
            {
                case Image image:
                    image.Service ??= new List<IService>();
                    image.Service.Add(serviceToAdd);
                    
                    // does this have an image service?
                    var iiifImageApi2 = image.Service.OfType<ImageService2>().SingleOrDefault();
                    if (iiifImageApi2 != null)
                    {
                        iiifImageApi2.Service ??= new List<IService>();
                        iiifImageApi2.Service.Add(serviceToAdd);
                    }
                    break;
                default:
                    resource.Service ??= new List<IService>();
                    resource.Service.Add(serviceToAdd);
                    break;
            }
        }


        public void Structures(Manifest manifest, IManifestation metsManifestation)
        {
            var physIdDict = metsManifestation.Sequence!.ToDictionary(
                pf => pf.Id, pf => pf.StorageIdentifier);
            
            // See MetsRepositoryPackageProvider, line 379, and https://digirati.atlassian.net/browse/WDL-97
            Range wdlRoot = MakeRangeFromMetsStructure(
                metsManifestation.Identifier!,
                physIdDict!,
                metsManifestation.RootStructRange!, 
                metsManifestation.ParentSectionMetadata);
            if (IsManuscriptStructure(metsManifestation.RootStructRange))
            {
                wdlRoot = ConvertFirstChildToRoot(wdlRoot);
            }
            // we now have the equivalent of old DDS Section - but the rootsection IS the equivalent
            // of the manifest, which we already have. We don't need a top level Range for everything,
            // we're only interested in Child structure.
            if (wdlRoot.Items.HasItems())
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
            var newRoot = (Range?) wdlRoot.Items?.FirstOrDefault(r => r is Range);
            if (newRoot == null) return wdlRoot;
            newRoot.Label = wdlRoot.Label;
            return newRoot;
        }

        private bool IsManuscriptStructure(IStructRange? rootStructRange)
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
                && rootStructRange.Children[0].PhysicalFileIds!.Count == rootStructRange.PhysicalFileIds!.Count
            );
            
        }

        private Range MakeRangeFromMetsStructure(
            string manifestationId,
            Dictionary<string, string> physIdDict,
            IStructRange structRange,
            ISectionMetadata? parentSectionMetadata)
        {
            var range = new Range
            {
                Id = uriPatterns.Range(manifestationId, structRange.Id!)
            };
            if (structRange.Type == "PeriodicalIssue" && parentSectionMetadata != null)
            {
                // for periodicals, some MODS data is held at the VOLUME level, 
                // which is the dmdSec referenced by the parent structural div
                MergeExtraPeriodicalVolumeData(structRange.SectionMetadata!, parentSectionMetadata);
            }

            var modsForAccessCondition = structRange.SectionMetadata ?? parentSectionMetadata;
            if (!range.Label.HasItems()) // && structRange.Mods != null)
            {
                range.Label = GetMappedRangeLabel(structRange);
            }
            
            // physIdDict contains the "significant" assets; we should only add these, not all the assets
            var canvases = new List<Canvas>(); // this was called sectionAssets, int list
            foreach (string physicalFileId in structRange.PhysicalFileIds!)
            {
                if (physIdDict.TryGetValue(physicalFileId, out var storageIdentifier))
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
                    .Select(child => MakeRangeFromMetsStructure(manifestationId, physIdDict, child, modsForAccessCondition))
                    .ToList();
                if (childRanges.HasItems())
                {
                    range.Items ??= new List<IStructuralLocation>();
                    range.Items.AddRange(childRanges);
                }
            }
            
            return range;
        }

        private LanguageMap? GetMappedRangeLabel(IStructRange structRange)
        {
            var s = structRange.SectionMetadata?.Title ?? structRange.Type;
            if (s.IsNullOrWhiteSpace())
            {
                return null;
            }
            var humanFriendly = manifestStructureHelper.GetHumanFriendlySectionLabel(s);
            return Lang.Map("none", humanFriendly); // TODO - "en" is often wrong.
        }
        
        /// <summary>
        /// For Periodicals, additional MODS data (typically security info) is carried in a different
        /// MODS section, so we need to incorporate it into the current section's MODS
        /// </summary>
        /// <param name="sectionMods">The MODS for the current structural section - the periodical issue</param>
        /// <param name="volumeMods">The MODS for the volume (one per METS file)</param>
        private void MergeExtraPeriodicalVolumeData(ISectionMetadata sectionMods, ISectionMetadata volumeMods)
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
            if (metsManifestation.Sequence!.SupportsSearch())
            {
                manifest.Annotations = new List<AnnotationPage>
                {
                    new()
                    {
                        Id = uriPatterns.ManifestAnnotationPageImagesWithVersion(metsManifestation.Identifier, 3),
                        Label = Lang.Map($"OCR-identified images and figures for {metsManifestation.Identifier}")
                    }
                };
                if (addAllContentAnnos)
                {
                    manifest.Annotations.Add(new()
                    {
                        Id = uriPatterns.ManifestAnnotationPageAllWithVersion(metsManifestation.Identifier, 3),
                        Label = Lang.Map($"All OCR-derived annotations for {metsManifestation.Identifier}")
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
            if (work.Parts.HasItems() && iiifResource is Collection collection)
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
            if (!work.ReferenceNumber.HasText())
            {
                return null;
            }
            
            if (manifestation != null || work.HasIIIFDigitalLocation())
            {
                // definitely a manifest
                return new Manifest
                {
                    Id = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber),
                    Label = Lang.Map(work.Title!),
                    Thumbnail = manifestation?.GetThumbnail()
                };
            }
            // maybe a manifest, if only it were digitised... but, maybe just an undigitised work, or child structure.
            if (work.TotalParts > 0)
            {
                return new Collection
                {
                    Id = uriPatterns.CollectionForAggregation("archives", work.ReferenceNumber),
                    Label = Lang.Map(work.Title!)
                };
            }

            return null;
        }

        public void CheckForCopyAndVolumeStructure(
            IManifestation metsManifestation,
            State? state)
        {
            if (metsManifestation.SectionMetadata!.CopyNumber > 0)
            {
                if (state == null)
                {
                    // TODO - don't control application flow through exceptions, come back to this
                    throw new IIIFBuildStateException(
                        $"State is required to build {metsManifestation.Identifier}");
                }
                state.MultiCopyState ??= new MultiCopyState();
                state.MultiCopyState.CopyAndVolumes[metsManifestation.Identifier] = 
                    new CopyAndVolume(metsManifestation.Identifier)
                    {
                        CopyNumber = metsManifestation.SectionMetadata.CopyNumber,
                        VolumeNumber = metsManifestation.SectionMetadata.VolumeNumber
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
                    ((Manifest) br.IIIFResource!).Items.HasItems() &&
                    ((Manifest) br.IIIFResource).Items!.Exists(c => c.Duration > 0));

            
            string newId = buildResults.Identifier;
            var avCanvases = new List<Canvas>();
            BuildResult? firstManifestationBuildResult = null;
            foreach (var relevantBuildResult in relevantBuildResults)
            {
                Manifest? manifest = (Manifest?) relevantBuildResult.IIIFResource;
                firstManifestationBuildResult ??= relevantBuildResult;
                var canvases = manifest!.Items!.Where(c => c.Duration > 0);
                // we now have the right Manifest, but it has the wrong Identifiers everywhere...
                string oldId = relevantBuildResult.Id;
                relevantBuildResult.Id = newId;
                manifest.Id = manifest.Id!.Replace(oldId, newId);
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
                        AddSupplementingPdfToCanvas(buildResults.Identifier, avCanvases[tCounter], transcripts[tCounter].Files![0], "transcript", "PDF Transcript");
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

        /// <summary>
        /// This is a suitable model for including a PDF transcript, but not for when the PDF is the main attraction.
        /// </summary>
        /// <param name="manifestIdentifier"></param>
        /// <param name="canvas"></param>
        /// <param name="pdfFile"></param>
        /// <param name="annoIdentifier"></param>
        /// <param name="label"></param>
        private void AddSupplementingPdfToCanvas(string manifestIdentifier, Canvas canvas, IStoredFile pdfFile,
            string annoIdentifier, string label)
        {
            var pageCountMetadata = GetPageCountMetadata(pdfFile.PhysicalFile!);
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
            canvas.Id = canvas.Id!.Replace(oldIdPath, newIdPath);
            var annoPage = canvas.Items![0];
            annoPage.Id = annoPage.Id!.Replace(oldIdPath, newIdPath);
            var anno = (PaintingAnnotation) annoPage.Items![0];
            anno.Id = anno.Id!.Replace(oldIdPath, newIdPath);
            if (anno.Target is ResourceBase target)
            {
                target.Id = target.Id!.Replace(oldIdPath, newIdPath);
            }

            if (!changeImageBody) return;
            if (anno.Body is Image image)
            {
                image.Id = image.Id!.Replace(oldId, newId);
            }
        }

        public void AddTrackingLabel(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var mdFormat = manifestationMetadata.Manifestations.FirstOrDefault()?.RootSectionType;
            var format = mdFormat.HasText() ? mdFormat : "n/a";

            var partner = PartnerAgents.GetPartner(manifestationMetadata.Metadata.GetLocationOfOriginal()!);
            var institution = partner != null ? partner.Label : "n/a";

            var mdDigicode = manifestationMetadata.Metadata.GetDigitalCollectionCodes().FirstOrDefault();
            var digicode = mdDigicode.HasText() ? mdDigicode : "n/a";
            
            var mdCalmRef = manifestationMetadata.Manifestations.FirstOrDefault()?.ReferenceNumber;
            var collectioncode = mdCalmRef.HasText() ? mdCalmRef : "n/a";

            string trackingLabel = "Format: " + format +
                        ", Institution: " + institution +
                        ", Identifier: " + manifestationMetadata.Identifier.PackageIdentifier +
                        ", Digicode: " + digicode +
                        ", Collection code: " + collectioncode;


            ((ICollectionItem)iiifResource).Services ??= new List<IService>();
            ((ICollectionItem)iiifResource).Services?.Add(
                new SimpleTextBasedService
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
                new SimpleTextBasedService
                {
                    Id = iiifResource.Id + "#timestamp",
                    Profile = Constants.Profiles.BuilderTime,
                    Label = Lang.Map("none", DateTime.UtcNow.ToString("O"))
                });
        }

        public void AddAccessHint(Manifest manifest, IManifestation metsManifestation, string identifier)
        {
            var accessConditions = metsManifestation.Sequence!
                .Select(pf => pf.AccessCondition).ToList();
            var mostSecureAccessCondition = AccessCondition.GetMostSecureAccessCondition(accessConditions!);
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
            var accessHintService = new SimpleTextBasedService
            {
                Id = manifest.Id + "#accesscontrolhints",
                Profile = Constants.Profiles.AccessControlHints,
                Label = Lang.Map(accessHint)
            };
            var accessHintGroups = accessConditions.GroupBy(ac => ac).ToList();
            foreach (var hintGroup in accessHintGroups)
            {
                if (hintGroup.Key.HasText())
                {
                    var label = hintGroup.Key;
                    if (label == AccessCondition.RequiresRegistration)
                    {
                        // Rename this old access condition for the public IIIF Manifest
                        label = AccessCondition.OpenWithAdvisory;
                    }
                    accessHintService.Metadata ??= new List<LabelValuePair>();
                    accessHintService.Metadata.AddNonlang(label, hintGroup.Count().ToString());
                }
            }
            manifest.Services?.Add(accessHintService);
            

        }
    }
}