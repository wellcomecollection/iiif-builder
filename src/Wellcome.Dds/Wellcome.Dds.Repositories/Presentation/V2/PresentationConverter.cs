using System;
using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V2;
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

            LegacyResourceBase p2Resource = null;

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
            manifest.Id = manifest.Id!.Replace("/presentation/", "/presentation/v2/");

            if (!p3Manifest.Structures.IsNullOrEmpty())
            {
                manifest.Structures = new List<Range>(p3Manifest.Structures!.Count);
                foreach (var r in p3Manifest.Structures ?? new List<Presi3.Range>())
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

            // TODO Sequences
            return manifest;
        }

        private T GetHydratedPresentationBase<T>(Presi3.StructureBase resourceBase)
            where T : IIIFPresentationBase, new()
        {
            var presentationBase = new T
            {
                Id = resourceBase.Id, // TODO - do these all need rewritten?
                Attribution = MetaDataValue.Create(resourceBase.RequiredStatement?.Label),
                Description = MetaDataValue.Create(resourceBase.Summary),
                Label = MetaDataValue.Create(resourceBase.Label),
                NavDate = resourceBase.NavDate,
                Metadata = ConvertMetadata(resourceBase.Metadata),
                License = resourceBase.Rights,
                Within = resourceBase.PartOf?.FirstOrDefault()?.Id,
                ViewingHint = resourceBase.Behavior?.FirstOrDefault(),
                // Profile = TODO for linking to external  
            };

            if (!resourceBase.Provider.IsNullOrEmpty())
            {
                
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
                    Label = MetaDataValue.Create(p.Label),
                    Value = MetaDataValue.Create(p.Value)
                })
                .ToList();
        }
    }
}