using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IIIF;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2
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
    
    public class AnnotationListForMedia : IIIFPresentationBase
    {
        /*[JsonProperty(Order = 12, PropertyName = "metadata")]
        public List<Metadata>? Metadata { get; set; }
        
        [JsonProperty(Order = 14, PropertyName = "metadata")]
        public string Thumbnail { get; set; }
        
        public string[] Rendering { get; set; }*/
        public override string? Type { get; set; }
        
        [JsonProperty(Order = 11)]
        public string? Format { get; set; }

        // TODO - Auth Services
        [JsonProperty(Order = 28)]
        [ObjectIfSingle]
        public List<IService>? Service { get; set; }
        
        [JsonProperty(Order = 71)]
        public int? Width { get; set; }
        
        [JsonProperty(Order = 72)]
        public int? Height { get; set; }
        
        // TODO Resources
    }
}