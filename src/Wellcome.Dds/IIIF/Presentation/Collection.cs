﻿using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
        /// The direction in which a set of Canvases should be displayed to the user
        /// See <a href="https://iiif.io/api/presentation/3.0/#viewingdirection">viewingdirection</a>
        /// </summary>
        [JsonProperty(Order = 101)]
        public string? ViewingDirection { get; set; }
        
        /// <summary>
        /// Note that this is not the same as ResourceBase::Service
        /// </summary>
        [JsonProperty(Order = 102)]
        public List<IService>? Services { get; set; }
        
        /// <summary>
        /// Embedded Collections or Referenced Manifests/Collections.
        /// </summary>
        // TODO - Interface may cause issues for deserialization
        [JsonProperty(Order = 103)]
        public List<ICollectionItem>? Items { get; set; }
    }
}
