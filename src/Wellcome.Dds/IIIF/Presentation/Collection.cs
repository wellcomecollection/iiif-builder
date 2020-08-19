using IIIF.Presentation.Services;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    /// <summary>
    /// Collections are used to list the <see cref="Manifest"/>s available for viewing.
    /// May include both other Collections and Manifests to form a tree-structured hierarchy.
    /// See <a href="https://iiif.io/api/presentation/3.0/#51-collection">Collection docs</a>.
    /// </summary>
    public class Collection : StructureBase, ICollectionItem
    {
        public override string Type => nameof(Collection);
        
        /// <summary>
        /// Embedded Collections or Referenced Manifests/Collections.
        /// </summary>
        // TODO - Interface may cause issues for deserialization
        public List<ICollectionItem>? Items { get; set; }
        
        /// <summary>
        /// The direction in which a set of Canvases should be displayed to the user
        /// See <a href="https://iiif.io/api/presentation/3.0/#viewingdirection">viewingdirection</a>
        /// </summary>
        public string? ViewingDirection { get; set; }
        
        // TODO - Interface may cause issues for deserialization
        public List<IService>? Services { get; set; }
    }
}
