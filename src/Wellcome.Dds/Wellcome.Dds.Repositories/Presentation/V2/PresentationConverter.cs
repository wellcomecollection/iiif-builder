using System;
using System.Collections.Generic;
using System.Linq;
using DeepCopy;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Guard;
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
        public LegacyResourceBase Convert(Presi3.StructureBase presentation, string identifier)
        {
            presentation.ThrowIfNull(nameof(presentation));
            identifier.ThrowIfNullOrEmpty(nameof(identifier));

            try
            {
                LegacyResourceBase p2Resource = presentation switch
                {
                    Presi3.Manifest p3Manifest => ConvertManifest(p3Manifest, identifier, true),
                    Presi3.Collection p3Collection => ConvertCollection(p3Collection, identifier),
                    _ => throw new ArgumentException(
                        $"Unable to convert {presentation.GetType()} to v2. Expected: Canvas or Manifest",
                        nameof(presentation))
                };
                
                p2Resource.EnsureContext(IIIF.Presentation.Context.V2);
                return p2Resource;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error converting bnumber '{BNumber}' to IIIF2", identifier);
                throw;
            }
        }

        private Collection ConvertCollection(Presi3.Collection p3Collection, string manifestId)
        {
            var collection = GetIIIFPresentationBase<Collection>(p3Collection);
            collection.Id = collection.Id!.Replace("/presentation/", "/presentation/v2/"); // TODO - find better way 
            if (p3Collection.Items.IsNullOrEmpty())
            {
                logger.LogWarning("Collection {CollectionId} has no items", p3Collection.Id);
                return collection;
            }
            
            collection.Manifests = p3Collection.Items
                !.OfType<Presi3.Manifest>()
                .Select(m => ConvertManifest(m, manifestId, false))
                .ToList();
            collection.Collections = p3Collection.Items
                .OfType<Presi3.Collection>()
                .Select(c => ConvertCollection(c, manifestId))
                .ToList();
            
            // TODO .Members?

            return collection;
        }

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest, string manifestId, bool rootResource)
        {
            // We only want "Volume X" and "Copy X" labels for Manifests within Collections
            var manifest = rootResource
                ? GetIIIFPresentationBase<Manifest>(p3Manifest)
                : GetIIIFPresentationBase<Manifest>(p3Manifest, s => s.StartsWith("Copy") || s.StartsWith("Volume"));

            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/"); // TODO - find better way

            if (!p3Manifest.Services.IsNullOrEmpty())
            {
                (manifest.Service ??= new List<IService>()).AddRange(DeepCopy(p3Manifest.Services)!);
            }

            if (!p3Manifest.Structures.IsNullOrEmpty())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest!.Structures)
                {
                    var range = GetIIIFPresentationBase<Range>(r);
                    range.ViewingDirection = r.ViewingDirection;

                    if (r.Start is Presi3.Canvas {Id: not null} canvas)
                        range.StartCanvas = new Uri(canvas.Id);
                    
                    range.Canvases = r.Items?.OfType<Presi3.Canvas>().Select(c => c.Id ?? string.Empty).ToList();
                    range.Ranges = r.Items?.OfType<Presi3.Range>().Select(c => c.Id ?? string.Empty).ToList();

                    manifest.Structures.Add(range);
                }
            }

            // Sequence
            // NOTE - there will only ever be 1 sequence
            if (p3Manifest.Items.IsNullOrEmpty()) return manifest;

            var canvases = new List<Canvas>(p3Manifest.Items!.Count);
            foreach (var p3Canvas in p3Manifest.Items)
            {
                var canvas = GetIIIFPresentationBase<Canvas>(p3Canvas);
                canvas.Height = canvas.Height;
                canvas.Width = canvas.Width;

                if (!p3Canvas.Items.IsNullOrEmpty())
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

                        images.Add(GetImageAnnotation(image, paintingAnnotation, canvas));
                    }

                    canvas.Images = images;
                }

                canvases.Add(canvas);
            }

            manifest.Sequences = new List<Sequence>
            {
                new()
                {
                    Id = uriPatterns.Sequence(manifestId, "seq0"),
                    Label = new MetaDataValue("Sequence s0"),
                    Rendering = p3Manifest.Rendering?
                        .Select(r => new Presi2.ExternalResource
                            {Format = r.Format, Id = r.Id, Label = MetaDataValue.Create(r.Label, true)})
                        .ToList(),
                    Canvases = canvases
                }
            };

            return manifest;
        }

        private ImageAnnotation GetImageAnnotation(Image image, PaintingAnnotation paintingAnnotation, Canvas canvas)
        {
            var imageServices = image.Service?.OfType<ImageService2>()
                .Select(i => DeepCopy(i, service2 =>
                {
                    service2.EnsureContext(ImageService2.Image2Context);
                    service2.Type = null;
                }))
                .Cast<IService>()
                .ToList();
            
            var imageAnnotation = new ImageAnnotation();
            imageAnnotation.Id = paintingAnnotation.Id;
            imageAnnotation.On = canvas.Id ?? string.Empty;
            imageAnnotation.Resource = new ImageResource
            {
                Id = image.Id,
                Height = image.Height,
                Width = image.Width,
                Format = image.Format,
                Service = imageServices
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
            presentationBase.ViewingHint = resourceBase.Behavior?.FirstOrDefault();
            presentationBase.Within = resourceBase.PartOf?.FirstOrDefault()?.Id;
            presentationBase.Service = DeepCopy(resourceBase.Service);
            presentationBase.Profile = resourceBase.Profile;
            presentationBase.Thumbnail = ConvertThumbnails(resourceBase.Thumbnail);

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                // NOTE - Logo can be an image-svc but we only support URI for now
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
            resource.Description = MetaDataValue.Create(externalResource.Label, true);
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
    }
}