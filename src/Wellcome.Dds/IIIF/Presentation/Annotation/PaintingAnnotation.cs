using Newtonsoft.Json;

namespace IIIF.Presentation.Annotation
{
    public class PaintingAnnotation : Annotation
    {
        public override string Motivation => Constants.Motivation.Painting;
        
        [JsonProperty(Order = 500)]
        public IPaintable? Body { get; set; }
    }
}
