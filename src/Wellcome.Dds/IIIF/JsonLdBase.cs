using Newtonsoft.Json;

namespace IIIF
{
    public abstract class JsonLdBase
    {
        // TODO - this can be List<string> or string - how will deserializer handle this? string[] or string? 
        [JsonProperty(Order = 1, PropertyName = "@context")]
        public object? Context { get; set; } // This one needs its serialisation name changing...
    }
}
