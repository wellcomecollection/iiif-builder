using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using DeepCopy;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Utils;
using Utils.Guard;
using Wellcome.Dds.IIIFBuilding;
using ExternalResource = IIIF.Presentation.V3.Content.ExternalResource;
using Presi3 = IIIF.Presentation.V3;
using Presi2 = IIIF.Presentation.V2;
using Range = IIIF.Presentation.V2.Range;

namespace Wellcome.Dds.Repositories.Presentation.V2
{
    public class PresentationConverter
    {
        private readonly ILogger<PresentationConverter> logger;

        public PresentationConverter()
        {
            // TODO - configure logger
            logger = NullLogger<PresentationConverter>.Instance;
        }
        
        public LegacyResourceBase Convert(Presi3.StructureBase presentation)
        {
            presentation.ThrowIfNull(nameof(presentation));

            LegacyResourceBase p2Resource;

            if (presentation is Presi3.Manifest p3Manifest)
            {
                p2Resource = ConvertManifest(p3Manifest, true);
            }
            else if (presentation is Presi3.Collection p3Collection)
            {
                p2Resource = ConvertCollection(p3Collection);
            }
            else
            {
                throw new ArgumentException($"Unable to convert {presentation.GetType()} to v2", nameof(presentation));
            }

            p2Resource.EnsureContext(IIIF.Presentation.Context.V2);
            return p2Resource;
        }

        private Collection ConvertCollection(Presi3.Collection p3Collection)
        {
            var collection = GetIIIFPresentationBase<Collection>(p3Collection);
            if (p3Collection.Items.IsNullOrEmpty())
            {
                logger.LogWarning("Collection {CollectionId} has no items", p3Collection.Id);
                return collection;
            }
            
            collection.Manifests = p3Collection.Items
                !.OfType<Presi3.Manifest>()
                .Select(m => ConvertManifest(m, false))
                .ToList();
            collection.Collections = p3Collection.Items.OfType<Presi3.Collection>().Select(ConvertCollection).ToList();

            return collection;
        }

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest, bool rootResource)
        {
            // We only want "Volume X" and "Copy X" labels for Manifests within Collections
            var manifest = rootResource
                ? GetIIIFPresentationBase<Manifest>(p3Manifest)
                : GetIIIFPresentationBase<Manifest>(p3Manifest, s => s.StartsWith("Copy") || s.StartsWith("Volume"));

            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/"); // TODO - find better way 

            if (!p3Manifest.Structures.IsNullOrEmpty())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest!.Structures)
                {
                    var range = GetIIIFPresentationBase<Range>(r);
                    range.ViewingDirection = r.ViewingDirection;

                    if (r.Start is Presi3.Canvas canvas) range.StartCanvas = new Uri(canvas!.Id);

                    // TODO - change the Ids here?
                    range.Canvases = r.Items?.OfType<Presi3.Canvas>().Select(c => c.Id ?? string.Empty).ToList();
                    range.Ranges = r.Items?.OfType<Presi3.Range>().Select(c => c.Id ?? string.Empty).ToList();

                    manifest.Structures.Add(range);
                }
            }

            // Sequence
            // NOTE - there will only ever be 1 sequence in output
            if (p3Manifest.Items.IsNullOrEmpty()) return manifest;

            var canvases = new List<Canvas>(p3Manifest.Items!.Count);
            foreach (var p3Canvas in p3Manifest!.Items)
            {
                var canvas = GetIIIFPresentationBase<Canvas>(p3Canvas);
                canvas.Height = canvas.Height;
                canvas.Width = canvas.Width;

                if (!p3Canvas.Items.IsNullOrEmpty())
                {
                    if (p3Canvas!.Items.Count > 1)
                        logger.LogWarning("'{ManifestId}', canvas '{CanvasId}' has more Items than expected",
                            p3Manifest.Id, p3Canvas.Id);

                    var annotations = p3Canvas.Items[0].Items;
                    var paintingAnnotations = annotations.OfType<PaintingAnnotation>().ToList();

                    if (annotations.Count > paintingAnnotations.Count)
                        logger.LogWarning("'{ManifestId}', canvas '{CanvasId}' has non-painting annotations",
                            p3Manifest.Id, p3Canvas.Id);

                    var images = new List<ImageAnnotation>(paintingAnnotations.Count);
                    foreach (var paintingAnnotation in paintingAnnotations)
                    {
                        var imageAnnotation =
                            ConvertPaintingAnnotation(p3Manifest, paintingAnnotation, p3Canvas, canvas);
                        if (imageAnnotation != null) images.Add(imageAnnotation);
                    }

                    canvas.Images = images;
                }

                canvases.Add(canvas);
            }

            manifest.Sequences = new List<Sequence>
            {
                new()
                {
                    Id = "/what/goes/here?", // TODO
                    Label = new MetaDataValue("Sequence s0"), // TODO,
                    Rendering = p3Manifest.Rendering?
                        .Select(r => new Presi2.ExternalResource
                            {Format = r.Format, Id = r.Id, Label = MetaDataValue.Create(r.Label, true)})
                        .ToList(),
                    Canvases = canvases
                }
            };

            return manifest;
        }

        private ImageAnnotation? ConvertPaintingAnnotation(Presi3.Manifest p3Manifest, PaintingAnnotation paintingAnnotation, Presi3.Canvas p3Canvas,
            Canvas canvas)
        {
            if (paintingAnnotation.Body is not Image image)
            {
                logger.LogWarning(
                    "'{ManifestId}', canvas '{CanvasId}', anno '{AnnotationId}' has non-painting annotations",
                    p3Manifest.Id, p3Canvas.Id, paintingAnnotation.Id);
                return null;
            }

            var imageServices = image.Service.OfType<ImageService2>()
                .Select(i => DeepCopy(i, service2 =>
                {
                    service2.EnsureContext(ImageService2.Image2Context);
                    // TODO - clear type - maybe in serializer? Custom for 2 or not if in p2?
                }))
                .Cast<IService>()
                .ToList();
            var imageAnnotation = new ImageAnnotation
            {
                Id = paintingAnnotation.Id, // TODO
                On = canvas.Id,
                Resource = new ImageResource
                {
                    Id = image.Id,
                    Height = image.Height,
                    Width = image.Width,
                    Format = image.Format,
                    Service = imageServices
                }
            };
            return imageAnnotation;
        }

        private T GetIIIFPresentationBase<T>(Presi3.StructureBase resourceBase, Func<string, bool>? labelFilter = null)
            where T : IIIFPresentationBase, new()
        {
            var presentationBase = new T
            {
                Id = resourceBase.Id, // TODO - do all Ids need rewritten? (only if dereferencable)
                Attribution = MetaDataValue.Create(resourceBase.RequiredStatement?.Label, true),
                Description = MetaDataValue.Create(resourceBase.Summary, true),
                License = resourceBase.Rights,
                Metadata = ConvertMetadata(resourceBase.Metadata),
                NavDate = resourceBase.NavDate,
                Related = resourceBase.Homepage?.Select(ConvertResource).ToList(),
                SeeAlso = resourceBase.SeeAlso?.Select(ConvertResource).ToList(),
                ViewingHint = resourceBase.Behavior?.FirstOrDefault(),
                Within = resourceBase.PartOf?.FirstOrDefault()?.Id,
                Service = DeepCopy(resourceBase.Service), // TODO - add legacy services used by wl.org
                Profile = resourceBase.Profile, // TODO - does this need modified?
                Thumbnail = resourceBase.Thumbnail?.Select(t => new Thumbnail
                {
                    Service = t.Service.OfType<ImageService2>()
                        .Select(i => DeepCopy(i, service2 =>
                        {
                            service2.EnsureContext(ImageService2.Image2Context);
                            // TODO - clear type - maybe in serializer? Custom for 2 or not if in p2?
                        }))
                        .Cast<IService>()
                        .ToList(),
                    Id = t.Id
                }).ToList()
            };

            presentationBase.Label = MetaDataValue.Create(resourceBase.Label, true, labelFilter);

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                // NOTE - Logo can be an image-svc but we only support URI for now
                presentationBase.Logo = resourceBase!.Provider.First().Logo?.FirstOrDefault()?.Id;
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

        private static Resource ConvertResource(ExternalResource externalResource)
            => new()
            {
                Id = externalResource.Id,
                Description = MetaDataValue.Create(externalResource.Label, true),
                Format = externalResource.Format, 
                Profile = externalResource.Profile,
                Service = DeepCopy(externalResource.Service)
            };
        
        private static List<Presi2.Metadata>? ConvertMetadata(List<LabelValuePair>? presi3Metadata)
        {
            if (presi3Metadata.IsNullOrEmpty()) return null;

            return presi3Metadata!
                .Select(p => new Presi2.Metadata
                {
                    Label = MetaDataValue.Create(p.Label, true),
                    Value = MetaDataValue.Create(p.Value, true)
                })
                .ToList();
        }

        private static T? DeepCopy<T>(T source, Action<T>? modifier = null)
        {
            var copy = DeepCopier.Copy(source, new CopyContext());
            modifier?.Invoke(copy);
            return source;
        }
    }
}