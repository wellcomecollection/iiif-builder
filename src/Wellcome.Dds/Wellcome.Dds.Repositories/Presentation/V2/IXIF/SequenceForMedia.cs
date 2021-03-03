using System.Collections.Generic;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// <see cref="Sequence"/> object for use in a <see cref="MediaManifest"/>
    /// </summary>
    public class SequenceForMedia : Sequence
    {
        [JsonProperty(Order = 48, PropertyName = "compatibilityHint")]
        public string CompatibilityHint = "displayIfContentUnsupported";

        [JsonIgnore]
        public static readonly SequenceForMedia Instance;

        static SequenceForMedia()
        {
            Instance = new SequenceForMedia
            {
                Id = $"{Constants.WellcomeCollectionUri}/iiif/ixif-message/sequence/seq",
                Label = new MetaDataValue(
                    "Unsupported extension. This manifest is being used as a wrapper for non-IIIF content (e.g., audio, video) and is unfortunately incompatible with IIIF viewers."),
                Canvases = new List<Canvas>(1)
                {
                    new()
                    {
                        Id = "https://wellcomelibrary.org/iiif/ixif-message/canvas/c1",
                        Label = new MetaDataValue("Placeholder image"),
                        Thumbnail = new List<Thumbnail>
                        {
                            new()
                            {
                                Id = $"{Constants.WellcomeCollectionUri}/placeholder.jpg",
                                Type = null
                            }
                        },
                        Height = 600,
                        Width = 600,
                        Images = new List<ImageAnnotation>(1)
                        {
                            new()
                            {
                                Id = $"{Constants.WellcomeCollectionUri}/iiif/ixif-message/imageanno/placeholder",
                                Resource = new ImageResource
                                {
                                    Id = $"{Constants.WellcomeCollectionUri}/iiif/ixif-message-0/res/placeholder",
                                    Height = 600,
                                    Width = 600
                                },
                                On = $"{Constants.WellcomeCollectionUri}/iiif/ixif-message/canvas/c1"
                            }
                        },
                    }
                }
            };
        }
    }
}