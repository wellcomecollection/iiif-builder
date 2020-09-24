using IIIF.Presentation.Annotation;

namespace IIIF.Presentation.Content
{
    public class Audio : ExternalResource, ITemporal, IPaintable
    {
        public double Duration { get; set; }

        public Audio() : base(nameof(Audio)) { }
    }
}