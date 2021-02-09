using IIIF.LegacyInclusions;
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
        
        // W3C (IIIF 3) Annotations
        // keys
        // - v3/{identifier}/all/line
        // - v3/{identifier}/images
        // - v3/{identifier}/{assetIdentifier}/line
        public AnnotationPage AllContentAnnotations { get; set; }
        public AnnotationPage ImageAnnotations { get; set; }
        public AnnotationPage[] PageAnnotations { get; set; }
        
        // Open Annotation (IIIF 2) Annotations
        // keys
        // - v2/{identifier}/all/line
        // - v2/{identifier}/images
        // We won't create page-level OA annotations.
        // DDS.Server will transform them on the fly from the v3 W3C version.
        public AnnotationList OpenAnnotationAllContentAnnotations { get; set; }
        public AnnotationList OpenAnnotationImageAnnotations { get; set; }
        
        public IManifestation Manifestation { get; set; }
    }
}