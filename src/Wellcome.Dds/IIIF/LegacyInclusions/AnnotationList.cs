﻿using IIIF.Presentation.Annotation;
using Newtonsoft.Json;

namespace IIIF.LegacyInclusions
{
    public class AnnotationList : LegacyResourceBase
    {
        public override string Type => "sc:AnnotationList";

        [JsonProperty(Order = 20, PropertyName = "resources")]
        public IAnnotation[] Resources { get; set; }
    }
}
