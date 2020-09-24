using IIIF.Presentation.Selectors;
using Newtonsoft.Json;

namespace IIIF.Presentation
{
    public class SpecificResource : ResourceBase, IStructuralLocation
    {
        public override string Type => nameof(SpecificResource);
        
        [JsonProperty(Order = 101)]
        public string Source { get; set; }
        
        [JsonProperty(Order = 102)]
        public ISelector Selector { get; set; } 
    }
}
