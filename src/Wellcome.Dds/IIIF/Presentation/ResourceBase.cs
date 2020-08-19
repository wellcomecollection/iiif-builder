using IIIF.Presentation.Content;
using IIIF.Presentation.Services;
using IIIF.Presentation.Strings;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    /// <summary>
    /// Base class for all IIIF presentation resources. 
    /// </summary>
    public abstract class ResourceBase
    {
        // TODO - this can be List<string> or string - how will deserializer handle this? string[] or string? 
        public object? Context { get; set; } // This one needs its serialisation name changing...
        
        /// <summary>
        /// The type or class of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#type">type</a>
        /// </summary>
        public abstract string Type { get; } 
        
        /// <summary>
        /// The URI that identifies the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#id">id</a>
        /// </summary>
        public string? Id { get; set; }
        
        /// <summary>
        /// A human readable label, name or title.
        /// See <a href="https://iiif.io/api/presentation/3.0/#label">Label</a>
        /// </summary>
        public LanguageMap? Label { get; set; }
        
        /// <summary>
        /// A short textual summary intended to be conveyed to the user when the metadata entries for the resource are
        /// not being displayed.
        /// See <a href="https://iiif.io/api/presentation/3.0/#summary">summary</a>
        /// </summary>
        public LanguageMap? Summary { get; set; }
        
        /// <summary>
        /// An ordered list of descriptions to be displayed to the user when they interact with the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#metadata">metadata</a>
        /// </summary>
        public List<LabelValuePair>? Metadata { get; set; }
        
        /// <summary>
        /// Text that must be displayed when the resource is displayed or used.
        /// See <a href="https://iiif.io/api/presentation/3.0/#requiredstatement">requiredstatement</a>
        /// </summary>
        public LabelValuePair? RequiredStatement { get; set; }
        
        /// <summary>
        /// A string that identifies a license or rights statement that applies to the content of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#rights">rights</a>
        /// </summary>
        public string? Rights { get; set; }
        
        /// <summary>
        /// An organization or person that contributed to providing the content of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#provider">provider</a>
        /// </summary>
        public List<Agent>? Provider { get; set; }
        
        /// <summary>
        /// A content resource that represents the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#thumbnail">thumbnail</a>
        /// </summary>
        public List<ExternalResource>? Thumbnail { get; set; }
        
        /// <summary>
        /// A schema or named set of functionality available from the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#profile">profile</a>
        /// </summary>
        public string? Profile { get; set; }
        
        /// <summary>
        /// A set of user experience features that the publisher of the content would prefer the client to use when
        /// presenting the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#behavior">behavior</a>
        /// </summary>
        public List<string>? Behavior { get; set; }
        
        /// <summary>
        /// A web page that is about the object represented by this resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#homepage">homepage</a>
        /// </summary>
        public List<ExternalResource>? HomePage { get; set; }
        
        /// <summary>
        /// A resource that is an alternative, non-IIIF representation of the resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#rendering">rendering</a>
        /// </summary>
        public List<ExternalResource>? Rendering { get; set; }
        
        // TODO - Interface may cause issues for deserialization
        public List<IService>? Service { get; set; }
        
        /// <summary>
        /// A machine-readable resource such as an XML or RDF description that is related to the current resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#seealso">seealso</a>
        /// </summary>
        public List<ExternalResource>? SeeAlso { get; set; }
        
        /// <summary>
        /// A containing resource that includes this resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#partof">partof</a>
        /// </summary>
        public List<ResourceBase>? PartOf { get; set; }
    }
}
