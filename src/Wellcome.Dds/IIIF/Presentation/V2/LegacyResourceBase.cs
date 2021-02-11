using System.Collections.Generic;
using IIIF.Presentation.V2.Annotation;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    /// <summary>
    /// Common base for all legacy/pre-v3 IIIF models.
    /// </summary>
    public abstract class LegacyResourceBase : JsonLdBase
    {
        [JsonProperty(PropertyName = "@id", Order = 2)]
        public string? Id { get; set; }

        [JsonProperty(PropertyName = "@type", Order = 3)]
        public abstract string Type { get; }
        
        [JsonProperty(Order = 4)]
        public string Profile { get; set; } 
        
        [JsonProperty(Order = 11, PropertyName = "label")]
        public MetaDataValue Label { get; set; }
        
        [JsonProperty(Order = 13, PropertyName = "description")]
        public MetaDataValue Description { get; set; }
    }
    
    /// <summary>
    /// A short, descriptive entry consisting of human readable label and value to be displayed to the user.
    /// </summary>
    /// <remarks>See https://iiif.io/api/presentation/2.1/#metadata</remarks>
    public class Metadata
    {
        [JsonProperty(Order = 1, PropertyName = "label")]
        public MetaDataValue Label { get; set; }

        [JsonProperty(Order = 2, PropertyName = "value")]
        public MetaDataValue Value { get; set; }
    }

    /// <summary>
    /// Base class, used as root to all IIIF v2 Presentation models.
    /// </summary>
    public abstract class IIIFPresentationBase : LegacyResourceBase
    {
        [JsonProperty(Order = 12, PropertyName = "metadata")]
        public List<Metadata> Metadata { get; set; }
        
        // TODO - what type?
        [JsonProperty(Order = 15, PropertyName = "thumbnail")]
        public dynamic Thumbnail { get; set; } 
        
        [JsonProperty(Order = 16, PropertyName = "attribution")]
        public MetaDataValue Attribution { get; set; }
        
        [JsonProperty(Order = 17, PropertyName = "license")]
        public string License { get; set; }

        [JsonProperty(Order = 18, PropertyName = "logo")]
        public string Logo { get; set; }

        // TODO - what type?
        [JsonProperty(Order = 24, PropertyName = "rendering")]
        public dynamic Rendering { get; set; }

        // TODO - what type?
        [JsonProperty(Order = 25, PropertyName = "related")]
        public Resource Related { get; set; }

        [JsonProperty(Order = 26, PropertyName = "seeAlso")]
        public dynamic SeeAlso { get; set; }

        // TODO - this has custom serialiser - do we need?
        [JsonProperty(Order = 27, PropertyName = "service")]
        public IService Service { get; set; }

        [JsonProperty(Order = 30, PropertyName = "viewingHint")]
        public string ViewingHint { get; set; }
        
        [JsonProperty(Order = 32, PropertyName = "navDate")]
        public string NavDate { get; set; }

        // TODO - what type?
        [JsonProperty(Order = 60, PropertyName = "otherContent")]
        public IAnnotationListReference[] OtherContent { get; set; }

        [JsonProperty(Order = 70, PropertyName = "within")]
        public string Within { get; set; }
    }

    public interface IService
    {
        IService Service { get; set; }
    }

    public abstract class ServiceBase : LegacyResourceBase, IService
    {
        public IService Service { get; set; }
    }
    
    public abstract class Resource : LegacyResourceBase
    {
        [JsonProperty(Order = 10, PropertyName = "format")]
        public string Format { get; set; }

        //public override string Type { get; }

        [JsonProperty(Order = 11, PropertyName = "profile")]
        public virtual dynamic Profile { get; set; }

        [JsonProperty(Order = 20, PropertyName = "label")]
        public MetaDataValue Label { get; set; }

        [JsonProperty(Order = 99, PropertyName = "service")]
        public dynamic Service { get; set; } // object or array of objects

    }
}