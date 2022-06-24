using System;
using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using Microsoft.Net.Http.Headers;

namespace Wellcome.Dds.Server.Conneg
{
    /// <summary>
    /// Contains IIIFPresentation related methods and constants.
    /// </summary>
    public static class IIIFPresentation
    {
        /// <summary>
        /// Contains Content-Type/Accepts headers for IIIF Presentation API. 
        /// </summary>
        public static class ContentTypes
        {
            /// <summary>
            /// Content-Type for IIIF presentation 2.
            /// </summary>
            public const string V2 = "application/ld+json;profile=\"" + Context.Presentation2Context + "\"";

            /// <summary>
            /// Content-Type for IIIF presentation 3. 
            /// </summary>
            public const string V3 = "application/ld+json;profile=\"" + Context.Presentation3Context + "\"";
        }

        /// <summary>
        /// Get <see cref="IIIFPresentationVersion"/> for provided mediaTypeHeader, favouring latest version.
        /// </summary>
        /// <param name="mediaTypeHeaders">Collection of <see cref="MediaTypeHeaderValue"/> objects.</param>
        /// <param name="fallbackVersion">Value to return if no specific version found.</param>
        /// <returns><see cref="IIIFPresentationVersion"/> derived from provided values.</returns>
        public static IIIF.Presentation.Version GetIIIFPresentationType(
            this IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders,
            IIIF.Presentation.Version fallbackVersion = IIIF.Presentation.Version.Unknown)
        {
            var mediaTypes = mediaTypeHeaders ?? Enumerable.Empty<MediaTypeHeaderValue>();
            
            // Get a list of all "profile" parameters, ordered to prefer most recent.
            var profiles = mediaTypes
                .Select(m =>
                    m.Parameters.SingleOrDefault(p =>
                        string.Equals(p.Name.Value, "profile", StringComparison.OrdinalIgnoreCase))?.Value.Value)
                .OrderByDescending(s => s);

            var v3Profile = $"\"{Context.Presentation3Context}\"";
            var v2Profile = $"\"{Context.Presentation2Context}\"";

            foreach (var profile in profiles)
            {
                if (string.IsNullOrEmpty(profile)) continue;
                if (profile == v3Profile)
                {
                    return IIIF.Presentation.Version.V3;
                }

                if (profile == v2Profile)
                {
                    return IIIF.Presentation.Version.V2;
                }
            }

            return fallbackVersion;
        }
    }
}