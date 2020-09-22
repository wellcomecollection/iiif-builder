using IIIF.Presentation.Content;

namespace IIIF.Presentation.Annotation
{
    public class SupplementingDocumentAnnotation : Annotation
    {
        public override string Motivation => Constants.Motivation.Supplementing;
        
        public ExternalResource Body { get; set; }
    }
}