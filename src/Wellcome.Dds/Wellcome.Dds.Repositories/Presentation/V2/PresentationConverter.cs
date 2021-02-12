using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using IIIF;
using IIIF.ImageApi.Service;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
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
        public LegacyResourceBase Convert(Presi3.StructureBase presentation)
        {
            presentation.ThrowIfNull(nameof(presentation));

            LegacyResourceBase p2Resource;

            if (presentation is Presi3.Manifest p3Manifest)
            {
                p2Resource = ConvertManifest(p3Manifest);
            }
            else if (presentation is Presi3.Collection prCollection)
            {
                throw new NotImplementedException("Collection not yet handled");
            }
            else
            {
                throw new ArgumentException($"Unable to convert {presentation.GetType()} to v2", nameof(presentation));
            }

            return p2Resource;
        }

        private Manifest ConvertManifest(Presi3.Manifest p3Manifest)
        {
            var manifest = GetHydratedPresentationBase<Manifest>(p3Manifest);
            manifest.ViewingDirection = p3Manifest.ViewingDirection;
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/"); // TODO - find better way 

            if (!p3Manifest.Structures.IsNullOrEmpty())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest!.Structures)
                {
                    // ensure context?
                    var range = GetHydratedPresentationBase<Range>(r);
                    range.ViewingDirection = r.ViewingDirection;

                    if (r.Start is Presi3.Canvas canvas) range.StartCanvas = new Uri(canvas!.Id);

                    // TODO - change the Ids here?
                    // Canvases
                    range.Canvases = r.Items?.OfType<Presi3.Canvas>().Select(c => c.Id).ToList();
                    range.Ranges = r.Items?.OfType<Presi3.Range>().Select(c => c.Id).ToList();

                    manifest.Structures.Add(range);
                }
            }

            // Sequence
            // NOTE - there will only ever be 1 sequence in output
            if (!p3Manifest.Items.IsNullOrEmpty())
            {
                var canvases = new List<Canvas>(p3Manifest.Items!.Count);
                foreach (var p3Canvas in p3Manifest!.Items)
                {
                    var canvas = GetHydratedPresentationBase<Canvas>(p3Canvas);
                    canvas.Height = canvas.Height;
                    canvas.Width = canvas.Width;

                    if (!p3Canvas.Items.IsNullOrEmpty())
                    {
                        var images = new List<ImageAnnotation>(p3Canvas.Items!.Count);

                        // TODO - a safety check here for length of non PaintingAnno
                        foreach (var paintingAnnotation in p3Canvas.Items[0].Items.OfType<PaintingAnnotation>())
                        {
                            if (paintingAnnotation.Body is not Image image)
                            {
                                // TODO - log
                                continue;
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
                            images.Add(imageAnnotation);
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
            }

            return manifest;
        }

        private T GetHydratedPresentationBase<T>(Presi3.StructureBase resourceBase)
            where T : IIIFPresentationBase, new()
        {
            var presentationBase = new T
            {
                Id = resourceBase.Id, // TODO - do all Ids need rewritten? (only if dereferencable)
                Attribution = MetaDataValue.Create(resourceBase.RequiredStatement?.Label, true),
                Description = MetaDataValue.Create(resourceBase.Summary, true),
                Label = MetaDataValue.Create(resourceBase.Label, true),
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

            presentationBase.EnsureContext(IIIF.Presentation.Context.V2);
            return presentationBase;
        }

        private static Resource ConvertResource(ExternalResource externalResource)
            => new()
            {
                Id = externalResource.Id,
                Description = MetaDataValue.Create(externalResource.Label),
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
            // TODO - implement
            modifier?.Invoke(source);
            return source;
        }
    }
}