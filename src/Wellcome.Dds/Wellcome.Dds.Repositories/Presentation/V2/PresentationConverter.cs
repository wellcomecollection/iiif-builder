using System;
using System.Collections.Generic;
using System.Linq;
using DeepCopy;
using IIIF;
using IIIF.Auth.V1;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Utils;
using Utils.Guard;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
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
        /// Convert P3 Collection or Manifest to P2 equivalent.
        /// </summary>
        /// <param name="presentation">Collection or Manifest to convert.</param>
        /// <param name="identifier">BNumber of collection/manifest</param>
        public ResourceBase Convert(Presi3.StructureBase presentation, DdsIdentifier? identifier)
        {
            presentation.ThrowIfNull(nameof(presentation));
            identifier.ThrowIfNull(nameof(identifier));

            try
            {
                ResourceBase p2Resource = presentation switch
                {
                    Presi3.Manifest p3Manifest => ConvertManifest(p3Manifest, identifier, true),
                    Presi3.Collection p3Collection => ConvertCollection(p3Collection, identifier!),
                    _ => throw new ArgumentException(
                        $"Unable to convert {presentation.GetType()} to v2. Expected: Canvas or Manifest",
                        nameof(presentation))
                };
                
                p2Resource.EnsurePresentation2Context();
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
            
            var collection = GetIIIFPresentationBase<Collection>(p3Collection);
            collection.ViewingHint = p3Collection.Behavior?.FirstOrDefault();
            collection.Id = collection.Id!.Replace("/presentation/", "/presentation/v2/"); 
            
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

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest, string? identifier, bool rootResource)
        {
            if (p3Manifest.Items.IsNullOrEmpty())
            {
                throw new InvalidOperationException($"Manifest {p3Manifest.Id} has no items");
            }
            
            // We only want "Volume X" and "Copy X" labels for Manifests within Collections
            var manifest = rootResource
                ? GetIIIFPresentationBase<Manifest>(p3Manifest)
                : GetIIIFPresentationBase<Manifest>(p3Manifest, s => s.StartsWith("Copy") || s.StartsWith("Volume"));

            // Store auth service - if we get this from p3Manifests.Services, we want to add to manifest.Services
            // but with some values stripped back (ConfirmLabel, Header etc).
            // however - we want the full object to FIRST canvas, and links thereafter
            WellcomeAuthService? wellcomeAuthService = null;

            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/");
            
            // TODO - will need to handle {"@context": "http://universalviewer.io/context.json"} uihints + tracking
            // services once these are added to IIIF3 manifest. Will be added to p3Manifest.Service

            if (!p3Manifest.Services.IsNullOrEmpty())
            {
                manifest.Service ??= new List<IService>();

                // P3 Services will all be auth-services
                foreach (var authService in p3Manifest.Services!)
                {
                    wellcomeAuthService = GetWellcomeAuthService(identifier, authService);

                    // Add a copy of the wellcomeAuthService to .Service - copy to ensure nulling fields doesn't
                    // null everywhere
                    manifest.Service.Add(DeepCopy(wellcomeAuthService, authService =>
                    {
                        foreach (var authCookieService in authService.AuthService.OfType<AuthCookieService>())
                        {
                            authCookieService.ConfirmLabel = null;
                            authCookieService.Header = null;
                            authCookieService.FailureHeader = null;
                            authCookieService.FailureDescription = null;
                        }
                    }));
                }
            }

            if (!p3Manifest.Structures.IsNullOrEmpty())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest!.Structures)
                {
                    var range = GetIIIFPresentationBase<Range>(r);
                    range.ViewingDirection = r.ViewingDirection;

                    // NOTE - this may break UV as it's a new prop
                    /*if (r.Start is Presi3.Canvas {Id: not null} canvas)
                        range.StartCanvas = new Uri(canvas.Id);*/
                    
                    range.Canvases = r.Items?.OfType<Presi3.Canvas>().Select(c => c.Id ?? string.Empty).ToList();
                    range.Ranges = r.Items?.OfType<Presi3.Range>().Select(c => c.Id ?? string.Empty).ToList();

                    manifest.Structures.Add(range);
                }
            }
            
            // NOTE - there will only ever be 1 sequence
            var canvases = new List<Canvas>(p3Manifest.Items!.Count);
            bool firstImage = true;
            foreach (var p3Canvas in p3Manifest.Items)
            {
                var canvas = GetIIIFPresentationBase<Canvas>(p3Canvas);
                if (p3Canvas.Duration.HasValue)
                {
                    throw new InvalidOperationException("A/V not yet supported!");
                }
                
                canvas.Height = p3Canvas.Height;
                canvas.Width = p3Canvas.Width;

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

                        images.Add(GetImageAnnotation(image, paintingAnnotation, canvas, wellcomeAuthService, firstImage));
                        firstImage = false;
                    }

                    canvas.Images = images;
                }

                canvases.Add(canvas);
            }

            manifest.Sequences = new List<Sequence>
            {
                new()
                {
                    Id = GetSequenceId(identifier, "s0"),
                    Label = new MetaDataValue("Sequence s0"),
                    Rendering = p3Manifest.Rendering?
                        .Select(r => new Presi2.ExternalResource
                            {Format = r.Format, Id = r.Id, Label = MetaDataValue.Create(r.Label, true)})
                        .ToList(),
                    Canvases = canvases, 
                    ViewingHint = p3Manifest.Behavior?.FirstOrDefault()
                }
            };

            return manifest;
        }

        private WellcomeAuthService GetWellcomeAuthService(string identifier, IService authService)
        {
            // All auth services are ResourceBase, we need to cast to access Context property to set it correctly
            if (authService is not ResourceBase authResourceBase)
            {
                throw new InvalidOperationException("Expected manifest.Services to only contain ResourceBase");
            }

            var copiedService = DeepCopy(authResourceBase, svc =>
            {
                svc.Type = null;
                svc.Context = Constants.IIIFAuthContext;
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
                    svc.Context = Constants.IIIFAuthContext;
                    if (svc.Label != null)
                    {
                        svc.Description = svc.Label;
                    }
                }
            }

            // Create a fale WellcomeAuthService to contain the above services
            var wellcomeAuthService = new WellcomeAuthService();
            wellcomeAuthService.Id = GetAuthServiceId(identifier);
            wellcomeAuthService.Profile = GetAuthServiceProfile(identifier);
            wellcomeAuthService.AuthService = new List<IService> {copiedService as IService};
            wellcomeAuthService.AccessHint = accessHint;
            wellcomeAuthService.EnsureContext("http://wellcomelibrary.org/ld/iiif-ext/0/context.json");
            return wellcomeAuthService;
        }

        private ImageAnnotation GetImageAnnotation(Image image, PaintingAnnotation paintingAnnotation, Canvas canvas,
            WellcomeAuthService? wellcomeAuthService, bool firstImage)
        {
            // Copy all services over, these will be ImageService2 and potentially ServiceReference for auth
            ImageService2? imageService = null;
            List<ServiceReference> serviceReference = new();
            var services = image.Service?
                .Select(i => DeepCopy(i, s =>
                {
                    switch (s)
                    {
                        case ImageService2 imageService2:
                        {
                            imageService2.EnsureContext(ImageService2.Image2Context);
                            imageService2.Type = null;

                            if (imageService2.Service.HasItems())
                            {
                                foreach (var resourceBaseService in imageService2.Service.OfType<ServiceReference>())
                                {
                                    resourceBaseService.Type = null;
                                }
                            }

                            imageService = imageService2;

                            break;
                        }
                        case ServiceReference svcRef:
                            svcRef.Type = null;

                            serviceReference.Add(svcRef);
                            break;
                    }
                }))
                .ToList();
            
            // We have an auth service and this is the first image on entire manifest
            if (wellcomeAuthService != null && firstImage)
            {
                // add the full auth service to ImageService.Service 
                imageService?.Service?.AddRange(wellcomeAuthService.AuthService);
                
                // removing serviceReference as this is to cookie service, which we've just embedded 
                imageService?.Service?.RemoveAll(s => s is ServiceReference);

                // and add it to the main Services collection
                services?.AddRange(wellcomeAuthService.AuthService);

                // but remove any ServiceReferences to click through
                services?.RemoveAll(s => serviceReference.Contains(s));
            }

            var imageAnnotation = new ImageAnnotation();
            imageAnnotation.Id = paintingAnnotation.Id;
            imageAnnotation.On = canvas.Id ?? string.Empty;
            imageAnnotation.Resource = new ImageResource
            {
                Id = image.Id,
                Height = image.Height,
                Width = image.Width,
                Format = image.Format,
                Service = services
            };
            return imageAnnotation;
        }

        private T GetIIIFPresentationBase<T>(Presi3.StructureBase resourceBase, Func<string, bool>? labelFilter = null)
            where T : IIIFPresentationBase, new()
        {
            // NOTE - using assignment statements rather than object initialiser to get line numbers for any errors
            var presentationBase = new T();
            presentationBase.Id = resourceBase.Id;
            presentationBase.Attribution = MetaDataValue.Create(resourceBase.RequiredStatement?.Label, true);
            presentationBase.Description = MetaDataValue.Create(resourceBase.Summary, true);
            presentationBase.Label = MetaDataValue.Create(resourceBase.Label, true, labelFilter);
            presentationBase.License = resourceBase.Rights;
            presentationBase.Metadata = ConvertMetadata(resourceBase.Metadata);
            presentationBase.NavDate = resourceBase.NavDate;
            presentationBase.Related = resourceBase.Homepage?.Select(ConvertResource).ToList();
            presentationBase.SeeAlso = resourceBase.SeeAlso?.Select(ConvertResource).ToList();
            presentationBase.Within = resourceBase.PartOf?.FirstOrDefault()?.Id;

            if (resourceBase.Service.HasItems())
            {
                presentationBase.Service = resourceBase.Service!.Select(s => DeepCopy(s, service =>
                {
                    if (service is ResourceBase serviceResourceBase)
                        serviceResourceBase.Type = null;
                    if (service is SearchService searchService)
                        searchService.EnsureContext(SearchService.Search1Context);
                })!).ToList();
            }

            presentationBase.Profile = resourceBase.Profile;
            presentationBase.Thumbnail = ConvertThumbnails(resourceBase.Thumbnail);

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                presentationBase.Logo = resourceBase.Provider!.First().Logo?.FirstOrDefault()?.Id;
            }
            
            if (!resourceBase.Annotations.IsNullOrEmpty())
            {
                presentationBase.OtherContent = resourceBase.Annotations?
                    .Select(a => new AnnotationListReference
                    {
                        Id = a.Id,
                        Label = MetaDataValue.Create(a.Label, true),
                    })
                    .Cast<IAnnotationListReference>()
                    .ToList();
            }
            
            return presentationBase;
        }

        private static List<Thumbnail>? ConvertThumbnails(List<ExternalResource>? thumbnails)
        {
            if (thumbnails.IsNullOrEmpty()) return null;

            return thumbnails!.Select(t => new Thumbnail
            {
                Service = t.Service?.OfType<ImageService2>()
                    .Select(i => DeepCopy(i, service2 =>
                    {
                        service2.EnsureContext(ImageService2.Image2Context);
                        service2.Type = null;
                    }))
                    .Cast<IService>()
                    .ToList(),
                Id = t.Id
            }).ToList();
        }

        private static Resource ConvertResource(ExternalResource externalResource)
        {
            var resource = new Resource();
            resource.Id = externalResource.Id;
            resource.Label = MetaDataValue.Create(externalResource.Label, true);
            resource.Format = externalResource.Format;
            resource.Profile = externalResource.Profile;
            resource.Service = DeepCopy(externalResource.Service);
            return resource;
        }

        private static List<Presi2.Metadata>? ConvertMetadata(List<LabelValuePair>? presi3Metadata)
        {
            if (presi3Metadata.IsNullOrEmpty()) return null;

            return presi3Metadata!
                .Select(p => new Presi2.Metadata
                {
                    Label = MetaDataValue.Create(p.Label, true)!,
                    Value = MetaDataValue.Create(p.Value, true)!
                })
                .ToList();
        }

        private static T? DeepCopy<T>(T source, Action<T>? postCopyModifier = null)
        {
            var copy = DeepCopier.Copy(source);
            postCopyModifier?.Invoke(copy);
            return copy;
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
    }
}