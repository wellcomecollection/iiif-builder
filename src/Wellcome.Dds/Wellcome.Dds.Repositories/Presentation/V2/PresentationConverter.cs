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
using Utils;
using Utils.Guard;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
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

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest, DdsIdentifier identifier, bool rootResource, int? sequence = 0)
        {
            if (rootResource && p3Manifest.Items.IsNullOrEmpty())
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
            WellcomeAuthServiceManager authServiceManager = new();

            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/");
            
            // TODO - will need to handle {"@context": "http://universalviewer.io/context.json"} uihints + tracking
            // services once these are added to IIIF3 manifest. Will be added to p3Manifest.Service

            if (p3Manifest.Services.HasItems())
            {
                manifest.Service ??= new List<IService>();

                // P3.Services will all be auth-services
                foreach (var authService in p3Manifest.Services!)
                {
                    var wellcomeAuthService = GetWellcomeAuthService(identifier, authService);

                    // Add a copy of the wellcomeAuthService to .Service - copy to ensure nulling fields doesn't
                    // null everywhere
                    manifest.Service.Add(DeepCopy(wellcomeAuthService, wellcomeAuth =>
                    {
                        foreach (var authCookieService in wellcomeAuth.AuthService.OfType<AuthCookieService>())
                        {
                            authCookieService.ConfirmLabel = null;
                            authCookieService.Header = null;
                            authCookieService.FailureHeader = null;
                            authCookieService.FailureDescription = null;
                        }
                    })!);
                    
                    // Add to wellcomeAuthServices collection, keyed by authService.id as we can use this to lookup later 
                    authServiceManager.Add(wellcomeAuthService);
                }
            }

            if (p3Manifest.Structures.HasItems())
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

            if (p3Manifest.Items.IsNullOrEmpty()) return manifest;

            // NOTE - there will only ever be 1 sequence
            var canvases = new List<Canvas>(p3Manifest.Items!.Count);
            foreach (var p3Canvas in p3Manifest.Items)
            {
                var canvas = GetIIIFPresentationBase<Canvas>(p3Canvas);
                if (p3Canvas.Duration.HasValue)
                {
                    throw new InvalidOperationException("A/V not yet supported!");
                }
                
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

                        images.Add(GetImageAnnotation(image, paintingAnnotation, canvas, authServiceManager));
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

            return manifest;
        }

        private WellcomeAuthService GetWellcomeAuthService(DdsIdentifier identifier, IService authService)
        {
            // All auth services are ResourceBase, we need to cast to access Context property to set it correctly
            if (authService is not ResourceBase authResourceBase)
            {
                throw new InvalidOperationException("Expected manifest.Services to only contain ResourceBase");
            }

            var copiedService = DeepCopy(authResourceBase, svc =>
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

        private ImageAnnotation GetImageAnnotation(Image image, PaintingAnnotation paintingAnnotation, Canvas canvas,
            WellcomeAuthServiceManager authServiceManager)
        {
            // Copy all services over, these will be ImageService2 and potentially ServiceReference for auth
            ImageService2? imageService = null;
            
            // Get all non-serviceReference services (service reference will be auth services)
            // we only want auth in ImageService
            var services = image.Service?
                .Where(s => s.GetType() != typeof(ServiceReference)).ToList()
                .Select(i => DeepCopy(i, s =>
                {
                    if (s is ImageService2 imageService2)
                    {
                        imageService2.EnsureContext(ImageService2.Image2Context);
                        imageService2.Type = null;
                        imageService2.Protocol = ImageService2.Image2Protocol;
                        imageService = imageService2;
                    }
                }))
                .ToList();
            
            // If we have auth services..
            if (authServiceManager.HasItems)
            {
                // Get all service references from image-service (will be auth services)
                // for P3 we will _only_ have svc refs, the main Auth services will be in the manifest.Service element 
                var authServiceReferences = imageService?.Service?.OfType<ServiceReference>().ToList();
                if (authServiceReferences.HasItems())
                {
                    // Remove these from imageService.Services
                    imageService?.Service?.RemoveAll(s => s is ServiceReference);

                    // Re-add appropriate services. This will be full AuthService if first time it appears,
                    // or serviceReference if it is not the first time
                    foreach (var authRef in authServiceReferences!)
                    {
                        var service = authServiceManager.Get(authRef.Id!);
                        if (service is WellcomeAuthService was)
                        {
                            // if we have a wellcomeAuthService add the "AuthService" only
                            imageService?.Service?.AddRange(was.AuthService);
                        }
                        else
                        {
                            // else just re-add the reference
                            imageService?.Service?.Add(service);
                        }
                    }
                }
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
                    {
                        searchService.EnsureContext(SearchService.Search1Context);
                        if (searchService.Service != null)
                            searchService.Service.Type = null;
                    }
                })!).ToList();
            }

            presentationBase.Profile = resourceBase.Profile;
            presentationBase.Thumbnail = ConvertThumbnails(resourceBase.Thumbnail);

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                // Wellcome will always be the first provider
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

            if (resourceBase.RequiredStatement != null)
            {
                AddAttributionAndUsageMetadata(resourceBase, presentationBase);
            }
            
            return presentationBase;
        }

        private static void AddAttributionAndUsageMetadata<T>(Presi3.StructureBase resourceBase, T presentationBase)
            where T : IIIFPresentationBase, new()
        {
            presentationBase.Metadata ??= new List<Presi2.Metadata>();

            var requiredStatement = resourceBase.RequiredStatement!.Value.SelectMany(rs => rs.Value).ToList();
            if (!requiredStatement.HasItems()) return;

            // Conditions of use is last section of requiredStatement
            var conditionsOfUse = requiredStatement.Count > 1 ? requiredStatement.Last() : string.Empty;

            // more than 1 provider means wellcome + _other_, so use other as attribution
            bool isNotWellcome = resourceBase.Provider!.Count > 1;
            var agent = resourceBase.Provider!.Last();

            // Attribution is the first element of the requiredStatement
            var attribution = isNotWellcome ? agent.Label!.ToString() : Constants.WellcomeCollection;

            // Check to see if we can get license to add to Attribution
            var license = LicenseMap.GetLicenseAbbreviation(presentationBase.License ?? string.Empty);
            if (string.IsNullOrEmpty(license))
            {
                if (conditionsOfUse.Contains(Constants.InCopyrightStatement))
                {
                    // "in copyright" works don't have a .License so need to look at conditions of use to determine value
                    license = Constants.InCopyrightCondition;
                }
                else if (conditionsOfUse.Contains(Constants.CopyrightNotClearedStatement))
                {
                    license = Constants.CopyrightNotClearedCondition;
                }
            }

            presentationBase.Metadata.Add(new Presi2.Metadata
            {
                Label = new MetaDataValue("Attribution"),
                Value = new MetaDataValue(string.IsNullOrEmpty(license)
                    ? attribution
                    : $"{attribution}<br/>License: {license}")
            });

            presentationBase.Metadata.Add(new Presi2.Metadata
            {
                Label = new MetaDataValue("Full conditions of use"),
                Value = new MetaDataValue(conditionsOfUse)
            });

            if (isNotWellcome)
            {
                // add repository if ! wellcome
                var logo = agent.Logo?.FirstOrDefault()?.Id;
                var licenseText = requiredStatement.First();
                var repository =
                    $"<img src='{logo}' alt='{attribution}' /><br/><br/>{licenseText}";
                presentationBase.Metadata.Add(new Presi2.Metadata
                {
                    Label = new MetaDataValue("Repository"),
                    Value = new MetaDataValue(repository)
                });
            }
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
                        service2.Protocol = ImageService2.Image2Protocol;
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