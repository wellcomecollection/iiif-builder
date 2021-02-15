using Newtonsoft.Json;

namespace IIIF.Presentation.V2.Annotation
{
    public class ContentAsTextAnnotationResource : LegacyResourceBase
    {
        public override string Type => "cnt:ContentAsText";

        [JsonProperty(Order = 10, PropertyName = "format")]
        public string Format { get; set; }

        [JsonProperty(Order = 20, PropertyName = "chars")]
        public string Chars { get; set; }
    }
}