using System.Collections.Generic;
using IIIF.Presentation.Content;
using Newtonsoft.Json;

namespace IIIF.Presentation
{
    public class Agent : ResourceBase
    {
        public override string Type => nameof(Agent);
        
        [JsonProperty(Order = 500)]
        public List<Image>? Logo { get; set; }
    }
}
