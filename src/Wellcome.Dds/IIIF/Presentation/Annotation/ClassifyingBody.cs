using IIIF.Presentation.Strings;

namespace IIIF.Presentation.Annotation
{
    public class ClassifyingBody : ResourceBase
    {
        public ClassifyingBody(string classifyingType)
        {
            Id = classifyingType;
        }
        
        public override string Type => null;
    }
}