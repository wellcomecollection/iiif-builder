using System.Collections.Generic;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    public class Resource : LegacyResourceBase
    {
        [JsonProperty(Order = 10, PropertyName = "format")]
        public string? Format { get; set; }

        [JsonProperty(Order = 99, PropertyName = "service")]
        public List<IService>? Service { get; set; } // object or array of objects

        public override string Type { get; }
    }
}