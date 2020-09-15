﻿using Newtonsoft.Json;

namespace IIIF.Presentation.Annotation
{
    public class Annotation : ResourceBase, IAnnotation
    {
        public override string Type => nameof(Annotation);
        
        [JsonProperty(Order = 10)]
        public virtual string? Motivation { get; set; }
        
        /// <summary>
        /// A mode associated with an Annotation that is to be applied to the rendering of any time-based media.
        /// see <a href="https://iiif.io/api/presentation/3.0/#timemode">timemode</a>
        /// </summary>
        [JsonProperty(Order = 21)]
        public string? TimeMode { get; set; }
        
    }
}
