using Newtonsoft.Json;

namespace IIIF.LegacyInclusions
{
    public abstract class LegacyResourceBase : JsonLdBase
    {
        [JsonProperty(PropertyName = "@id", Order = 2)]
        public string? Id { get; set; }

        [JsonProperty(PropertyName = "@type", Order = 3)]
        public abstract string Type { get; }
        
        [JsonProperty(Order = 4)]
        public string Profile { get; set; } 
        
        [JsonProperty(Order = 11, PropertyName = "label")]
        public MetaDataValue Label { get; set; }
        
        [JsonProperty(Order = 12, PropertyName = "description")]
        public MetaDataValue Description { get; set; }
    }
}