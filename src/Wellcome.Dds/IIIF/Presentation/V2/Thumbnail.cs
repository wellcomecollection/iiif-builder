using System.Collections.Generic;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    public class Thumbnail : LegacyResourceBase
    {
        public override string Type => "dctypes:Image";
        
        [JsonProperty(Order = 28)]
        public List<IService>? Service { get; set; }
    }
}