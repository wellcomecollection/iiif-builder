 using IIIF.Presentation.Annotation;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class Range : StructureBase, IStructuralLocation
    {
        public override string Type => nameof(Range);
        
        // TODO - Interface may cause issues for deserialization
        public List<IStructuralLocation>? Items { get; set; }
        
        public string? ViewingDirection { get; set; }
        
        public AnnotationCollection? Supplementary { get; set; }
        
        // TODO - Interface may cause issues for deserialization
        public IStructuralLocation? Start { get; set; }
    }
}
