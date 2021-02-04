namespace IIIF.Presentation.Annotation
{
    public class TypeClassifyingAnnotation : Annotation
    {
        public override string Motivation => Constants.Motivation.Classifying;
        
        public ResourceBase? Body { get; set; }
    }
}