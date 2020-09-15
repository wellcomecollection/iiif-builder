﻿ using IIIF.Presentation.Annotation;
using System.Collections.Generic;
 using Newtonsoft.Json;

 namespace IIIF.Presentation
{
    public class Range : StructureBase, IStructuralLocation
    {
        public override string Type => nameof(Range);
        
        [JsonProperty(Order = 32)]
        public string? ViewingDirection { get; set; }
        
        [JsonProperty(Order = 35)]
        public IStructuralLocation? Start { get; set; }
        
        [JsonProperty(Order = 300)]
        public List<IStructuralLocation>? Items { get; set; }
        
        [JsonProperty(Order = 400)]
        public AnnotationCollection? Supplementary { get; set; }
    }
}
