using IIIF.Presentation.Annotation;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    /// <summary>
    /// A Canvas represents an individual page or view and acts as a central point for assembling the different content
    /// resources that make up the display.
    /// See <a href="https://iiif.io/api/presentation/3.0/#53-canvas">Canvas docs</a>.
    /// </summary>
    public class Canvas : StructureBase, IStructuralLocation, IPaintable // but not ISpatial or ITemporal - that's for content
    {
        public override string Type => nameof(Canvas);

        /// <summary>
        /// The Width of the Canvas. This value does not have a unit.
        /// </summary>
        public int? Width { get; set; }
        
        /// <summary>
        /// The Height of the Canvas. This value does not have a unit.
        /// </summary>
        public int? Height { get; set; }
        
        /// <summary>
        /// The Duration of the Canvas, in seconds.
        /// </summary>
        public double? Duration { get; set; }
        
        public List<AnnotationPage>? Items { get; set; }
    }
}
