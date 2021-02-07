﻿using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Search.V1
{    
    /// <summary>
    /// Each AnnotationList references a Layer.  The Layer can be a blank node, and must be included in every annotation list. 
    /// </summary>
    public class SearchResultsLayer : LegacyResourceBase, IHasIgnorableParameters
    {
        public override string Type => "sc:Layer";

        [JsonProperty(Order = 10, PropertyName = "total")]
        public int Total { get; set; }

        [JsonProperty(Order = 14, PropertyName = "first")]
        public string First { get; set; }

        [JsonProperty(Order = 15, PropertyName = "last")]
        public string Last { get; set; }

        [JsonProperty(Order = 20, PropertyName = "ignored")]
        public string[] Ignored { get; set; }
    }
}
