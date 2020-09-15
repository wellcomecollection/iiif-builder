using Newtonsoft.Json;

namespace IIIF.Presentation.Content
{
    public class Image : ExternalResource, ISpatial
    {
        [JsonProperty(Order = 11)]
        public int Width { get; set; }
        
        [JsonProperty(Order = 12)]
        public int Height { get; set; }

        public Image() : base(nameof(Image)) {}
    }
}
