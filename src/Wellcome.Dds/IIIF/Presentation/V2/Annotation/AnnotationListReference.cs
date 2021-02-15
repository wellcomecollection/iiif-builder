using IIIF.Presentation.V2.Strings;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2.Annotation
{
    public class AnnotationListReference : LegacyResourceBase, IAnnotationListReference
    {
        public override string Type => "sc:AnnotationList";

        [JsonProperty(Order = 40, PropertyName = "label")]
        public MetaDataValue? Label { get; set; }
    }
}