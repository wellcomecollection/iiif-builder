using IIIF.Presentation.Annotation;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.IIIFBuilding
{
    public class AltoAnnotationBuildResult
    {
        public AltoAnnotationBuildResult(IManifestation manifestation)
        {
            Manifestation = manifestation;
        }
        
        // keys
        // - v3/{identifier}/all/line
        // - v3/{identifier}/images
        // - v3/{identifier}/{assetIdentifier}/line
        
        public AnnotationPage AllContentAnnotations { get; set; }
        public string AllContentAnnotationsKey { get; set; }
        public AnnotationPage ImageAnnotations { get; set; }
        public string ImageAnnotationsKey { get; set; }
        public AnnotationPage[] PageAnnotations { get; set; }
        public string[] PageAnnotationsKeys { get; set; }
        
        public IManifestation Manifestation { get; set; }
    }
}