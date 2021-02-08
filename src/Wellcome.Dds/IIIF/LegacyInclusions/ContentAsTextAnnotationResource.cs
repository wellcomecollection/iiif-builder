using Newtonsoft.Json;

namespace IIIF.LegacyInclusions
{
    public class ContentAsTextAnnotationResource : LegacyResourceBase
    {
        public override string Type => "cnt:ContentAsText";
        
        [JsonProperty(Order = 20, PropertyName = "chars")]
        public string Chars { get; set; }
    }
}