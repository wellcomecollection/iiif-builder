namespace IIIF.Presentation.Annotation
{
    public class PaintingAnnotation : Annotation
    {
        public IPaintable? Body { get; set; }
        public override string Motivation => Constants.Motivation.Painting;
    }
}
