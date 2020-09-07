using IIIF.Presentation.Services;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class Manifest : StructureBase, ICollectionItem
    {
        public override string Type => nameof(Manifest);
        public List<Canvas>? Items { get; set; }
        public List<Range>? Structures { get; set; }
        
        /// <summary>
        /// The direction in which a set of Canvases should be displayed to the user
        /// See <a href="https://iiif.io/api/presentation/3.0/#viewingdirection">viewingdirection</a>
        /// </summary>
        public string? ViewingDirection { get; set; }
        
        /// <summary>
        /// Note that this is not the same as ResourceBase::Service
        /// </summary>
        public List<IService>? Services { get; set; }
        
        // TODO - Interface may cause issues for deserialization
        public IStructuralLocation? Start { get; set; }
    }
}
