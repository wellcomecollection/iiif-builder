using System.Collections.Generic;
using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    public class ResourceForMedia : Resource
    {
        [JsonProperty(Order = 12, PropertyName = "metadata")]
        public List<IIIF.Presentation.V2.Metadata> Metadata { get; set; } = new();
        
        [JsonProperty(Order = 14, PropertyName = "thumbnail")]
        public string? Thumbnail { get; set; }
    }
}