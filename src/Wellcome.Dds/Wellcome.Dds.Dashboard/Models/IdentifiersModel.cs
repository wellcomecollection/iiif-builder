using System.Text.Json;
using System.Text.Json.Serialization;
using Utils;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Dashboard.Models
{
    /// <summary>
    /// Backs the Identifiers page: either a single explicit query against the Identifiers API,
    /// or a shape-based resolution of an arbitrary string.
    /// </summary>
    public class IdentifiersModel
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string BaseUrl { get; set; }
        public bool HasApiKey { get; set; }

        // Form inputs
        public string CanonicalId { get; set; }
        public string SourceSystem { get; set; }
        public string Value { get; set; }
        public string Type { get; set; } = OntologyType.Work;
        public bool IncludeSiblings { get; set; }
        public string ResolveInput { get; set; }

        // Single query results
        public string ApiUrl { get; set; }
        public IdentifierSet IdentifierSet { get; set; }
        public CanonicalIdRef CanonicalIdRef { get; set; }
        public bool NotFound { get; set; }
        public string ErrorMessage { get; set; }

        // Resolution results
        public IdentifierResolution Resolution { get; set; }

        public string[] KnownSourceSystems => SourceSystems.Known;
        public string[] Types => OntologyType.All;

        public bool HasQuery => ApiUrl.HasText();

        public static string ToJson(object o)
        {
            return JsonSerializer.Serialize(o, JsonOptions);
        }
    }
}
