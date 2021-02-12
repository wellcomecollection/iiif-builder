using System;
using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Strings;
using Utils;
using Utils.Guard;
using Wellcome.Dds.IIIFBuilding;
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

                    // Ranges
                    
                    // Canvases

                    manifest.Structures.Add(range);
                }
            }

            // TODO Sequences.
            // Add rendering from Manifest to first Sequence
            /*if (addRendering)
            {
                // Rendering is only added to first sequence element
                presentationBase.Rendering = resourceBase.Rendering?
                    .Select(r => new ExternalResource
                        {Format = r.Format, Id = r.Id, Label = MetaDataValue.Create(r.Label, true)})
                    .ToList();
            }*/
            
            return manifest;
        }

        private T GetHydratedPresentationBase<T>(Presi3.StructureBase resourceBase)
            where T : IIIFPresentationBase, new()
        {
            var presentationBase = new T
            {
                Id = resourceBase.Id, // TODO - do all Ids need rewritten?
                Attribution = MetaDataValue.Create(resourceBase.RequiredStatement?.Label, true),
                Description = MetaDataValue.Create(resourceBase.Summary, true),
                Label = MetaDataValue.Create(resourceBase.Label, true),
                License = resourceBase.Rights,
                Metadata = ConvertMetadata(resourceBase.Metadata),
                NavDate = resourceBase.NavDate,
                Related = resourceBase.Homepage?.Select(h => new Resource{Id = h.Id, Format = h.Format}).ToList(),
                SeeAlso = resourceBase.SeeAlso?.Select(sa => new Resource{Id = sa.Id, Format = sa.Format}).ToList(),
                ViewingHint = resourceBase.Behavior?.FirstOrDefault(),
                Within = resourceBase.PartOf?.FirstOrDefault()?.Id,

                // Service =
                // Profile = TODO for linking to external  
                // Thumbnail = TODO handle only if Canvas? 
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

        private List<Presi2.Metadata> ConvertMetadata(List<LabelValuePair>? presi3Metadata)
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
    }
}