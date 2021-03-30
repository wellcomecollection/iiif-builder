using System.Collections.Generic;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    public class AnnotationListForMedia : IIIFPresentationBase
    {
        public override string? Type { get; set; }
        
        [JsonProperty(Order = 11)]
        public string? Format { get; set; }
        
        [JsonProperty(Order = 71)]
        public int? Width { get; set; }
        
        [JsonProperty(Order = 72)]
        public int? Height { get; set; }

        [JsonProperty(Order = 28)]
        public List<Annotation> Resources { get; set; } = new();
    }
}