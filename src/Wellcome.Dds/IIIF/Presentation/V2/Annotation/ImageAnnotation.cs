using Newtonsoft.Json;

namespace IIIF.Presentation.V2.Annotation
{
    public class ImageAnnotation : LegacyResourceBase
    {
        public override string Type => "oa:Annotation";

        [JsonProperty(Order = 4, PropertyName = "motivation")]
        public string Motivation => "sc:painting";

        [JsonProperty(Order = 10, PropertyName = "resource")]
        public ImageResource Resource { get; set; }

        [JsonProperty(Order = 36, PropertyName = "on")]
        public string On { get; set; }
    }
}