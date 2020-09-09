﻿using IIIF.Presentation.Content;
using IIIF.Presentation.Services;
using IIIF.Presentation.Strings;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IIIF.Presentation
{
    /// <summary>
    /// Base class for all IIIF presentation resources. 
    /// </summary>
    public abstract class ResourceBase
    {
        // TODO - this can be List<string> or string - how will deserializer handle this? string[] or string? 
        [JsonProperty(Order = 1, PropertyName = "@context")]
        public object? Context { get; set; } // This one needs its serialisation name changing...
        
        /// <summary>
        /// The URI that identifies the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#id">id</a>
        /// </summary>
        [JsonProperty(Order = 2)]
        public string? Id { get; set; }
        
        /// <summary>
        /// The type or class of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#type">type</a>
        /// </summary>
        [JsonProperty(Order = 3)]
        public abstract string Type { get; } 
        
        
        /// <summary>
        /// A human readable label, name or title.
        /// See <a href="https://iiif.io/api/presentation/3.0/#label">Label</a>
        /// </summary>
        [JsonProperty(Order = 4)]
        public LanguageMap? Label { get; set; }
        
        /// <summary>
        /// A content resource that represents the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#thumbnail">thumbnail</a>
        /// </summary>
        [JsonProperty(Order = 5)]
        public List<ExternalResource>? Thumbnail { get; set; }
        
        /// <summary>
        /// A short textual summary intended to be conveyed to the user when the metadata entries for the resource are
        /// not being displayed.
        /// See <a href="https://iiif.io/api/presentation/3.0/#summary">summary</a>
        /// </summary>
        [JsonProperty(Order = 6)]
        public LanguageMap? Summary { get; set; }
        
        /// <summary>
        /// A web page that is about the object represented by this resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#homepage">homepage</a>
        /// </summary>
        [JsonProperty(Order = 7)]
        public List<ExternalResource>? HomePage { get; set; }
        
        /// <summary>
        /// An ordered list of descriptions to be displayed to the user when they interact with the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#metadata">metadata</a>
        /// </summary>
        [JsonProperty(Order = 8)]
        public List<LabelValuePair>? Metadata { get; set; }
        
        /// <summary>
        /// A string that identifies a license or rights statement that applies to the content of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#rights">rights</a>
        /// </summary>
        [JsonProperty(Order = 9)]
        public string? Rights { get; set; }
        
        /// <summary>
        /// Text that must be displayed when the resource is displayed or used.
        /// See <a href="https://iiif.io/api/presentation/3.0/#requiredstatement">requiredstatement</a>
        /// </summary>
        [JsonProperty(Order = 10)]
        public LabelValuePair? RequiredStatement { get; set; }
        
        
        /// <summary>
        /// An organization or person that contributed to providing the content of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#provider">provider</a>
        /// </summary>
        [JsonProperty(Order = 11)]
        public List<Agent>? Provider { get; set; }
        
        
        
        
        /// <summary>
        /// A resource that is an alternative, non-IIIF representation of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#rendering">rendering</a>
        /// </summary>
        [JsonProperty(Order = 21)]
        public List<ExternalResource>? Rendering { get; set; }
        
        // TODO - Interface may cause issues for deserialization
        [JsonProperty(Order = 22)]
        public List<IService>? Service { get; set; }
        
        /// <summary>
        /// A machine-readable resource such as an XML or RDF description that is related to the current resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#seealso">seealso</a>
        /// </summary>
        [JsonProperty(Order = 23)]
        public List<ExternalResource>? SeeAlso { get; set; }
        
        /// <summary>
        /// A containing resource that includes this resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#partof">partof</a>
        /// </summary>
        [JsonProperty(Order = 24)]
        public List<ResourceBase>? PartOf { get; set; }
        
        
        /// <summary>
        /// A set of user experience features that the publisher of the content would prefer the client to use when
        /// presenting the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#behavior">behavior</a>
        /// </summary>
        [JsonProperty(Order = 31)]
        public List<string>? Behavior { get; set; }
        
        /// <summary>
        /// A schema or named set of functionality available from the resource.
        /// The profile can further clarify the type and/or format of an external resource or service,
        /// allowing clients to customize their handling of the resource that has the profile property.
        /// See <a href="https://iiif.io/api/presentation/3.0/#profile">profile</a>
        /// </summary>
        [JsonProperty(Order = 33)]
        public string? Profile { get; set; }
    }
}
