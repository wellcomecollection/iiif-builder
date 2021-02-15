using System.Collections.Generic;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    /// <summary>
    /// Base class, used as root to all IIIF v2 Presentation models.
    /// </summary>
    public abstract class IIIFPresentationBase : LegacyResourceBase
    {
        [JsonProperty(Order = 12, PropertyName = "metadata")]
        public List<Metadata>? Metadata { get; set; }
        
        // TODO - what type? this seems too specific
        [JsonProperty(Order = 15, PropertyName = "thumbnail")]
        public List<Thumbnail>? Thumbnail { get; set; } 
        
        [JsonProperty(Order = 16, PropertyName = "attribution")]
        public MetaDataValue? Attribution { get; set; }
        
        [JsonProperty(Order = 17, PropertyName = "license")]
        public string? License { get; set; }

        [JsonProperty(Order = 18, PropertyName = "logo")]
        public string? Logo { get; set; }

        [JsonProperty(Order = 24, PropertyName = "rendering")]
        public List<ExternalResource>? Rendering { get; set; }

        // TODO - what type?
        [JsonProperty(Order = 25, PropertyName = "related")]
        public List<Resource>? Related { get; set; }

        [JsonProperty(Order = 26, PropertyName = "seeAlso")]
        public List<Resource>? SeeAlso { get; set; }

        // TODO - this has custom serialiser - do we need?
        [JsonProperty(Order = 27, PropertyName = "service")]
        public List<IService>? Service { get; set; }

        [JsonProperty(Order = 30, PropertyName = "viewingHint")]
        public string? ViewingHint { get; set; }
        
        [JsonProperty(Order = 32, PropertyName = "navDate")]
        public string? NavDate { get; set; }

        // TODO - what type?
        [JsonProperty(Order = 60, PropertyName = "otherContent")]
        public List<IAnnotationListReference>? OtherContent { get; set; }

        [JsonProperty(Order = 70, PropertyName = "within")]
        public string? Within { get; set; }
    }
}