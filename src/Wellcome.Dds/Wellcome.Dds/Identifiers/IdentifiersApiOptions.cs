using Utils;

namespace Wellcome.Dds.Identifiers
{
    public class IdentifiersApiOptions
    {
        /// <summary>
        /// The versioned root of the API, with no trailing slash.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.wellcomecollection.org/identifiers/v1";

        /// <summary>
        /// Sent as the x-api-key header; validated by API Gateway. Identifies this consumer.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Whether the DDS uses the Identifiers API at all. Unset, it is enabled exactly when an API key
        /// is configured, so an environment with no key (production, until a key is provisioned) behaves
        /// as it did before the API existed: registry lookups are skipped and the catalogue API and the
        /// storage service are used alone. Set false to switch the API off even with a key present.
        /// </summary>
        public bool? Enabled { get; set; }

        public bool IsEnabled => Enabled ?? ApiKey.HasText();
    }

    /// <summary>
    /// The request URLs, shared by the real client and the disabled stand-in so the dashboard can show
    /// what would be asked even where the API is off.
    /// </summary>
    public static class IdentifiersApiUrls
    {
        private const string IncludeSiblings = "siblings";

        public static string Canonical(string baseUrl, string canonicalId)
        {
            return $"{baseUrl.TrimEnd('/')}/{System.Uri.EscapeDataString(canonicalId.Trim())}";
        }

        public static string BySource(string baseUrl, string sourceSystem, string value, string type = OntologyType.Work, bool includeSiblings = false)
        {
            // value must be a single path segment, so a CALM ref like PPCRI/A/1 is sent as PPCRI%2FA%2F1
            var url = $"{baseUrl.TrimEnd('/')}/by-source/{System.Uri.EscapeDataString(sourceSystem.Trim())}/{System.Uri.EscapeDataString(value.Trim())}";
            var args = new System.Collections.Generic.List<string>();
            if (type.HasText())
            {
                args.Add($"type={System.Uri.EscapeDataString(type.Trim())}");
            }
            if (includeSiblings)
            {
                args.Add($"include={IncludeSiblings}");
            }
            return args.Count > 0 ? url + "?" + string.Join('&', args) : url;
        }
    }
}
