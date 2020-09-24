using IIIF.Presentation.Annotation;

namespace IIIF.Presentation.Content
{
    public class Video : ExternalResource, ISpatial, ITemporal, IPaintable
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double Duration { get; set; }

        public Video() : base(nameof(Video)) { }
    }
}