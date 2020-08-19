using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Net.Http.Headers;

namespace Wellcome.Dds.Server.Conneg
{
    /// <summary>
    /// Contains IIIFPresentation related methods and constants.
    /// </summary>
    /// TODO - move this to IIIF project once it exists. Should IIIFPresentation be a ns rather than class? 
    public static class IIIFPresentation
    {
        /// <summary>
        /// Contains JSON-LD Contexts for IIIF Presentation API.
        /// </summary>
        public static class Context
        {
            /// <summary>
            /// JSON-LD context for IIIF presentation 2.
            /// </summary>
            public const string V2 = "http://iiif.io/api/presentation/2/context.json";

            /// <summary>
            /// JSON-LD context for IIIF presentation 3. 
            /// </summary>
            public const string V3 = "http://iiif.io/api/presentation/3/context.json";
        }

        /// <summary>
        /// Contains Content-Type/Accepts headers for IIIF Presentation API. 
        /// </summary>
        public static class ContentTypes
        {
            /// <summary>
            /// Content-Type for IIIF presentation 2.
            /// </summary>
            public const string V2 = "application/ld+json;profile=\"" + Context.V2 + "\"";

            /// <summary>
            /// Content-Type for IIIF presentation 3. 
            /// </summary>
            public const string V3 = "application/ld+json;profile=\"" + Context.V3 + "\"";
        }

        /// <summary>
        /// Get <see cref="IIIFPresentationVersion"/> for provided mediaTypeHeader, favouring latest version.
        /// </summary>
        /// <param name="mediaTypeHeaders">Collection of <see cref="MediaTypeHeaderValue"/> objects.</param>
        /// <param name="fallbackVersion">Value to return if no specific version found.</param>
        /// <returns><see cref="IIIFPresentationVersion"/> derived from provided values.</returns>
        public static IIIFPresentationVersion GetIIIFPresentationType(
            this IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders,
            IIIFPresentationVersion fallbackVersion = IIIFPresentationVersion.Unknown)
        {
            var mediaTypes = mediaTypeHeaders ?? Enumerable.Empty<MediaTypeHeaderValue>();
            
            // Get a list of all "profile" parameters, ordered to prefer most recent.
            var profiles = mediaTypes
                .Select(m =>
                    m.Parameters.SingleOrDefault(p =>
                        string.Equals(p.Name.Value, "profile", StringComparison.OrdinalIgnoreCase))?.Value.Value)
                .OrderByDescending(s => s);

            var v3Profile = $"\"{Context.V3}\"";
            var v2Profile = $"\"{Context.V2}\"";

            foreach (var profile in profiles)
            {
                if (string.IsNullOrEmpty(profile)) continue;
                if (profile == v3Profile)
                {
                    return IIIFPresentationVersion.V3;
                }

                if (profile == v2Profile)
                {
                    return IIIFPresentationVersion.V2;
                }
            }

            return fallbackVersion;
        }
    }

    /// <summary>
    /// Available IIIF presentation API Versions.
    /// </summary>
    public enum IIIFPresentationVersion
    {
        /// <summary>
        /// Fallback value, unknown version.
        /// </summary>
        [Display(Description = "Unknown")]
        Unknown = 0,
        
        /// <summary>
        /// IIIF Presentation version 2.
        /// </summary>
        [Display(Description = IIIFPresentation.Context.V2)]
        V2,
        
        /// <summary>
        /// IIIF Presentation version 3.
        /// </summary>
        [Display(Description = IIIFPresentation.Context.V3)]
        V3
    }
}