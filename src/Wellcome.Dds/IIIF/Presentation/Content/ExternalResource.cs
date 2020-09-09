using IIIF.Presentation.Annotation;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IIIF.Presentation.Content
{
    public class ExternalResource : ResourceBase, IPaintable
    {
        public ExternalResource(string type)
        {
            Type = type;
        }

        public override string Type { get; }

        /// <summary>
        /// The specific media type (MIME type) for a content resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#format">format</a>
        /// </summary>
        /// <remarks>Only content resources may have the Format property</remarks>
        [JsonProperty(Order = 101)]
        public string? Format { get; set; }

        /// <summary>
        /// The language or languages used in the content of this external resource.
        /// See <a href="https://iiif.io/api/presentation/3.0/#language">language</a>
        /// </summary>
        [JsonProperty(Order = 102)]
        public List<string>? Language { get; set; }

        [JsonProperty(Order = 103)]
        public List<AnnotationPage>? Annotations { get; set; }
    }
}
