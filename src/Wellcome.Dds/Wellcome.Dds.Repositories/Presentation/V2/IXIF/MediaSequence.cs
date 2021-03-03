using System.Collections.Generic;
using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// ResourceBase to handle AV and BornDigital resources in IIIF P2.
    /// </summary>
    public class MediaSequence : ResourceBase
    {
        public override string? Type
        {
            get => "ixif:MediaSequence";
            set => throw new System.NotImplementedException();
        }

        [JsonProperty(Order = 15, PropertyName = "elements")]
        public List<AnnotationListForMedia> Elements { get; set; } = new();
    }
}