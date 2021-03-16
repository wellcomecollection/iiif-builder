using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.Auth.V1;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Guard;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.V2.IXIF;
using Annotation = IIIF.Presentation.V2.Annotation.Annotation;
using ExternalResource = IIIF.Presentation.V3.Content.ExternalResource;
using Presi3 = IIIF.Presentation.V3;
using Presi2 = IIIF.Presentation.V2;
using Range = IIIF.Presentation.V2.Range;

namespace Wellcome.Dds.Repositories.Presentation.V2
{
    /// <summary>
    /// Contains logic for converting IIIF P3 to IIIF P2.
    /// </summary>
    public class PresentationConverter
    {
        private readonly UriPatterns uriPatterns;
        private readonly ILogger logger;

        public PresentationConverter(UriPatterns uriPatterns, ILogger logger)
        {
            this.uriPatterns = uriPatterns;
            this.logger = logger;
        }

        /// <summary>
        /// Convert all supplied BuildResults to P2 equivalents.
        /// </summary>
        public MultipleBuildResult ConvertAll(string identifier, IEnumerable<BuildResult> buildResults)
        {
            var multipleBuildResult = new MultipleBuildResult();
            logger.LogDebug("Building LegacyIIIF for Id '{Identifier}'", identifier);

            int count = 0;
            foreach (var buildResult in buildResults)
            {
                if (buildResult.IIIFVersion == IIIF.Presentation.Version.V3 && buildResult.IIIFResource is Presi3.StructureBase iiif3)
                {
                    var result = new BuildResult(buildResult.Id, IIIF.Presentation.Version.V2);
                    try
                    {
                        var iiif2 = Convert(iiif3, buildResult.Id, count);
                        result.IIIFResource = iiif2;
                        result.Outcome = BuildOutcome.Success;

                        if (iiif2 is Manifest) count++;
                    }
                    catch (Exception e)
                    {
                        result.Message = e.Message;
                        result.Outcome = BuildOutcome.Failure;
                    }
                    multipleBuildResult.Add(result);
                }
                else
                {
                    logger.LogWarning("BuildLegacyIIIF called with non-IIIF3 BuildResult. Id: '{Identifier}'",
                        identifier);
                }
            }

            return multipleBuildResult;
        }

        /// <summary>
        /// Convert P3 Collection or Manifest to P2 equivalent.
        /// </summary>
        /// <param name="presentation">Collection or Manifest to convert.</param>
        /// <param name="identifier">BNumber of collection/manifest</param>
        /// <param name="sequence">Index of this particular manifest in a sequence</param>
        public ResourceBase Convert(Presi3.StructureBase presentation, DdsIdentifier identifier, int sequence = 0)
        {
            presentation.ThrowIfNull(nameof(presentation));
            identifier.ThrowIfNull(nameof(identifier));

            try
            {
                ResourceBase p2Resource = presentation switch
                {
                    Presi3.Manifest p3Manifest => ConvertManifest(p3Manifest, identifier, true, sequence),
                    Presi3.Collection p3Collection => ConvertCollection(p3Collection, identifier!),
                    _ => throw new ArgumentException(
                        $"Unable to convert {presentation.GetType()} to v2. Expected: Canvas or Manifest",
                        nameof(presentation))
                };
                
                // Do this at serialisation time
                // p2Resource.EnsurePresentation2Context();
                return p2Resource;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error converting bnumber '{BNumber}' to IIIF2", identifier);
                throw;
            }
        }

        private Collection ConvertCollection(Presi3.Collection p3Collection, DdsIdentifier identifier)
        {
            if (p3Collection.Items.IsNullOrEmpty())
            {
                throw new InvalidOperationException($"Collection {p3Collection.Id} has no items");
            }
            
            var collection = ConverterHelpers.GetIIIFPresentationBase<Collection>(p3Collection);
            collection.ViewingHint = p3Collection.Behavior?.FirstOrDefault();
            collection.Id = ConverterHelpers.ToPresentationV2Id(collection.Id);

            collection.Manifests = p3Collection.Items
                !.OfType<Presi3.Manifest>()
                .Select(m => ConvertManifest(m, identifier, false))
                .ToList();
            collection.Collections = p3Collection.Items
                .OfType<Presi3.Collection>()
                .Select(c => ConvertCollection(c, identifier))
                .ToList();

            return collection;
        }

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest, DdsIdentifier identifier, bool rootResource,
            int? sequence = 0)
            => Presi3.ManifestX.ContainsAV(p3Manifest) || ConverterHelpers.IsBornDigital(p3Manifest)
                ? ConvertManifest<MediaManifest>(p3Manifest, identifier, rootResource, sequence)
                : ConvertManifest<Manifest>(p3Manifest, identifier, rootResource, sequence);

        private T ConvertManifest<T>(Presi3.Manifest p3Manifest, DdsIdentifier identifier, bool rootResource, int? sequence = 0)
            where T : Manifest, new()
        {
            if (rootResource && p3Manifest.Items.IsNullOrEmpty())
            {
                throw new InvalidOperationException($"Manifest {p3Manifest.Id} has no items");
            }
            
            // We only want "Volume X" and "Copy X" labels for Manifests within Collections
            var manifest = rootResource
                ? ConverterHelpers.GetIIIFPresentationBase<T>(p3Manifest)
                : ConverterHelpers.GetIIIFPresentationBase<T>(p3Manifest, s => s.StartsWith("Copy") || s.StartsWith("Volume"));

            // Store auth service - if we get this from p3Manifests.Services, we want to add to manifest.Services
            // but with some values stripped back (ConfirmLabel, Header etc).
            // however - we want the full object to FIRST canvas that uses that auth, and links thereafter
            WellcomeAuthServiceManager authServiceManager = new();

            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = ConverterHelpers.ToPresentationV2Id(manifest.Id);
            
            if (p3Manifest.Services.HasItems())
            {
                manifest.Service ??= new List<IService>();

                // P3.Services will be auth-services or "made up" UV external resource
                bool haveAuth = false;
                foreach (var service in p3Manifest.Services!)
                {
                    switch (service)
                    {
                        case ResourceBase authService:
                            var wellcomeAuthService = GetWellcomeAuthService(identifier, authService);

                            // Add a copy of the wellcomeAuthService to .Service - copy to ensure nulling fields doesn't
                            // null all references
                            manifest.Service.Add(ObjectCopier.DeepCopy(wellcomeAuthService, wellcomeAuth =>
                            {
                                foreach (var authCookieService in wellcomeAuth.AuthService.OfType<AuthCookieService>())
                                {
                                    authCookieService.ConfirmLabel = null;
                                    authCookieService.Header = null;
                                    authCookieService.FailureHeader = null;
                                    authCookieService.FailureDescription = null;
                                }
                            })!);

                            // Add to wellcomeAuthServices collection
                            authServiceManager.Add(wellcomeAuthService);
                            haveAuth = true;
                            break;
                        case ExternalResource externalResource:
                            var legacyService = LegacyServiceFactory.GetLegacyService(identifier, externalResource);
                            if (legacyService != null)
                            {
                                // Only add AccessControlHints if we don't already have a wellcomeAuthService
                                if (legacyService is AccessControlHints && haveAuth) continue;
                                
                                manifest.Service.Add(legacyService);
                            }

                            break;
                        default:
                            logger.LogWarning(
                                "Unsure how to handle p3Manifest.Services service of type {Type} for {Identifier}",
                                service, identifier);
                            break;
                    }
                }
            }

            if (p3Manifest.Structures.HasItems())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest!.Structures)
                {
                    var range = ConverterHelpers.GetIIIFPresentationBase<Range>(r);
                    range.ViewingDirection = r.ViewingDirection;

                    // NOTE - this may break UV as it's a new prop
                    /*if (r.Start is Presi3.Canvas {Id: not null} canvas)
                        range.StartCanvas = new Uri(canvas.Id);*/
                    
                    range.Canvases = r.Items?.OfType<Presi3.Canvas>().Select(c => c.Id ?? string.Empty).ToList();
                    range.Ranges = r.Items?.OfType<Presi3.Range>().Select(c => c.Id ?? string.Empty).ToList();

                    manifest.Structures.Add(range);
                }
            }

            if (p3Manifest.Items.IsNullOrEmpty()) return manifest;

            if (manifest is MediaManifest mediaManifest)
            {
                SetMediaSequences(p3Manifest, identifier, authServiceManager, mediaManifest);
                mediaManifest.EnsureContext("http://wellcomelibrary.org/ld/ixif/0/context.json");
            }
            else
            {
                SetImageSequences(p3Manifest, identifier, sequence, authServiceManager, manifest);
            }

            return manifest;
        }

        private void SetMediaSequences(Presi3.Manifest p3Manifest, DdsIdentifier identifier,
            WellcomeAuthServiceManager authServiceManager, MediaManifest manifest)
        {
            // add a single Sequence as this is set in stone
            manifest.Sequences = new List<Sequence>(1) {SequenceForMedia.Instance};

            // Add a multipleManifestation per item - this won't necessarily work in UV but replicates current DDS
            var itemCount = 0; 
            foreach (var p3Canvas in p3Manifest.Items!)
            {
                var mediaSequence = new MediaSequence
                {
                    Id = GetMediaSequenceIdProfile(identifier, $"s{itemCount}"),
                    Label = new MetaDataValue($"XSequence {itemCount++}"),
                };

                var elements = new AnnotationListForMedia();
                elements.Label = manifest.Label;
                elements.Thumbnail = new List<Thumbnail>
                {
                    new() {Id = uriPatterns.PdfThumbnail(identifier), Type = null}
                };

                // Assumption is always 0 or 1 item (0 for born-digital, 1 for av)
                var item = p3Canvas.Items?.SingleOrDefault()?.Items?.FirstOrDefault();
                bool isBornDigital = false;
                if (item != null)
                {
                    if (item is not PaintingAnnotation paintingAnnotation)
                    {
                        throw new InvalidOperationException("Expected a painting annotation for AV");
                    }

                    // Populate Rendering, Id, Type, Format etc from PaintingAnnotation body
                    ConverterHelpers.PopulateFromBody(elements, paintingAnnotation.Body!, authServiceManager);
                }
                else
                {
                    isBornDigital = true;
                }
                
                // .. and maybe 1 annotation, if there's a transcription or it's born-digital
                var anno = p3Canvas.Annotations?.FirstOrDefault();
                if (anno != null)
                {
                    var annotation = new Annotation();
                    annotation.Id = anno.Id;
                    annotation.Motivation = "oad:transcribing";
                    annotation.On = elements.Id;

                    if (anno.Items.FirstOrDefault() is SupplementingDocumentAnnotation {Body: var body} &&
                        body is ExternalResource extBody)
                    {
                        var pageCount = manifest.Metadata.GetValueByLabel("Number of pages");
                        Presi2.Metadata? pageCountMeta = null;
                        if (!string.IsNullOrEmpty(pageCount))
                        {
                            pageCountMeta = new Presi2.Metadata
                            {
                                Label = new MetaDataValue("pages"),
                                Value = new MetaDataValue(pageCount)
                            };
                        }
                        
                        // if this is a born-digital item we need to use annotation to populate mediaSequences
                        if (isBornDigital)
                        {
                            elements.Id = body.Id;
                            elements.Type ="foaf:Document";
                            elements.Format = extBody.Format;
                           
                            if (pageCountMeta != null)
                            {
                                elements.Metadata ??= new List<Presi2.Metadata>();
                                elements.Metadata.Add(pageCountMeta);
                            }
                        }
                        else
                        {
                            var resource = new ResourceForMedia();
                            resource.Id = body.Id;
                            resource.Type = "foaf:Document";
                            resource.Format = extBody.Format;
                            resource.Label = manifest.Label;
                            resource.Thumbnail = uriPatterns.PdfThumbnail(identifier);
                            annotation.Resource = resource;
                            elements.Resources.Add(annotation);
                            if (pageCountMeta != null)
                            {
                                resource.Metadata.Add(pageCountMeta);
                            }
                        }
                    }
                    else
                    {
                        // TODO - what do we want to do here?
                        logger.LogWarning("Unexpected annotation item type");
                    }
                }
                
                mediaSequence.Elements.Add(elements);
                manifest.MediaSequences.Add(mediaSequence);
            }
        }

        private void SetImageSequences(Presi3.Manifest p3Manifest, DdsIdentifier identifier, int? sequence,
            WellcomeAuthServiceManager authServiceManager, Manifest manifest)
        {
            // NOTE - there will only ever be 1 sequence
            var canvases = new List<Canvas>(p3Manifest.Items!.Count);
            foreach (var p3Canvas in p3Manifest.Items)
            {
                var canvas = ConverterHelpers.GetIIIFPresentationBase<Canvas>(p3Canvas);

                canvas.Height = p3Canvas.Height;
                canvas.Width = p3Canvas.Width;
                canvas.ViewingHint = p3Canvas.Behavior?.FirstOrDefault();

                if (p3Canvas.Items.HasItems())
                {
                    if (p3Canvas.Items!.Count > 1)
                        logger.LogWarning("'{ManifestId}', canvas '{CanvasId}' has more Items than expected",
                            p3Manifest.Id, p3Canvas.Id);

                    var annotations = p3Canvas.Items[0].Items;
                    var paintingAnnotations = annotations!.OfType<PaintingAnnotation>().ToList();

                    if (annotations.Count > paintingAnnotations.Count)
                        logger.LogWarning("'{ManifestId}', canvas '{CanvasId}' has non-painting annotations",
                            p3Manifest.Id, p3Canvas.Id);

                    var images = new List<ImageAnnotation>(paintingAnnotations.Count);
                    foreach (var paintingAnnotation in paintingAnnotations)
                    {
                        if (paintingAnnotation.Body is not Image image)
                        {
                            logger.LogWarning(
                                "'{ManifestId}', canvas '{CanvasId}', anno '{AnnotationId}' has non-painting annotations",
                                p3Manifest.Id, p3Canvas.Id, paintingAnnotation.Id);
                            continue;
                        }

                        images.Add(ConverterHelpers.GetImageAnnotation(image, paintingAnnotation, canvas,
                            authServiceManager));
                    }

                    canvas.Images = images;
                }

                canvases.Add(canvas);
            }

            var sequenceId = $"s{sequence ?? 0}";
            manifest.Sequences = new List<Sequence>
            {
                new()
                {
                    Id = GetSequenceId(identifier, sequenceId),
                    Label = new MetaDataValue($"Sequence {sequenceId}"),
                    Rendering = p3Manifest.Rendering?
                        .Select(r => new Presi2.ExternalResource
                            {Format = r.Format, Id = r.Id, Label = MetaDataValue.Create(r.Label, true)})
                        .ToList(),
                    Canvases = canvases,
                    ViewingHint = p3Manifest.Behavior?.FirstOrDefault()
                }
            };
        }

        private WellcomeAuthService GetWellcomeAuthService(DdsIdentifier identifier, ResourceBase authResourceBase)
        {
            var copiedService = ObjectCopier.DeepCopy(authResourceBase, svc =>
            {
                svc.Type = null;
                svc.Context = IIIF.Auth.V1.Constants.IIIFAuthContext;
            })!;

            // The access hint is the last section of current profile
            var accessHint =
                copiedService.Profile?.Substring(
                    copiedService.Profile.LastIndexOf("/", StringComparison.Ordinal) + 1);

            // this is a list but there'll only be 1
            if (copiedService is AuthCookieService cookieService)
            {
                // Remove @type and set @context in all sub-services
                // we've already copied these so these are fine to edit
                foreach (var svc in cookieService.Service.OfType<ResourceBase>())
                {
                    svc.Type = null;
                    svc.Context = IIIF.Auth.V1.Constants.IIIFAuthContext;
                    if (svc.Label != null)
                    {
                        svc.Description = svc.Label;
                    }
                }
            }

            // Create a fake WellcomeAuthService to contain the above services
            var wellcomeAuthService = new WellcomeAuthService();
            wellcomeAuthService.Id = GetAuthServiceId(identifier);
            wellcomeAuthService.Profile = GetAuthServiceProfile(identifier);
            wellcomeAuthService.AuthService = new List<IService> {(IService)copiedService};
            wellcomeAuthService.AccessHint = accessHint;
            wellcomeAuthService.EnsureContext($"{Constants.WellcomeCollectionUri}/ld/iiif-ext/0/context.json");
            return wellcomeAuthService;
        }

        private string GetSequenceId(DdsIdentifier identifier, string sequenceIdentifier)
        {
            const string sequenceIdentifierToken = "{sequenceIdentifier}";
            const string sequenceFormat = "/presentation/v2/{identifier}/sequences/{sequenceIdentifier}";

            return uriPatterns.GetPath(sequenceFormat, identifier, (sequenceIdentifierToken, sequenceIdentifier));
        } 
        
        private string GetAuthServiceId(DdsIdentifier identifier)
        {
            const string authServiceIdFormat = "/iiif/{identifier}-0/access-control-hints-service";
            return uriPatterns.GetPath(authServiceIdFormat, identifier);
        }
        
        private string GetAuthServiceProfile(DdsIdentifier identifier)
        {
            const string authServiceProfileFormat = "/ld/iiif-ext/access-control-hints";
            return uriPatterns.GetPath(authServiceProfileFormat, identifier);
        }
        
        private string GetMediaSequenceIdProfile(DdsIdentifier identifier, string sequenceIdentifier)
        {
            const string sequenceIdentifierToken = "{sequenceIdentifier}";
            const string mediaSequenceFormat = "/iiif/{identifier}/xsequence/{sequenceIdentifier}";
            return uriPatterns.GetPath(mediaSequenceFormat, identifier, (sequenceIdentifierToken, sequenceIdentifier));
        }
    }
}