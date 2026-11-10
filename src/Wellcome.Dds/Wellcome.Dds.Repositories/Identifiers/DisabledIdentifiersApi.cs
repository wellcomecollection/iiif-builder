using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Repositories.Identifiers
{
    /// <summary>
    /// Registered in place of the real client where the Identifiers API is not enabled (see
    /// <see cref="IdentifiersApiOptions.IsEnabled"/>): every lookup answers "no mapping" without a request,
    /// and a resolution says it was skipped, so the navigator, the dashboard and, later, the identity write
    /// path all behave as they did before the API existed. Nothing throws.
    /// </summary>
    public class DisabledIdentifiersApi : IIdentifiersApi
    {
        public const string Reason = "The Identifiers API is not enabled in this environment (no IdentifiersApi:ApiKey, or IdentifiersApi:Enabled is false)";

        private readonly string baseUrl;

        public DisabledIdentifiersApi(IOptions<IdentifiersApiOptions> options)
        {
            baseUrl = options.Value.BaseUrl;
        }

        public bool IsEnabled => false;

        public Task<IdentifierSet?> GetByCanonicalId(string canonicalId) => Task.FromResult<IdentifierSet?>(null);

        public Task<CanonicalIdRef?> GetBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work)
            => Task.FromResult<CanonicalIdRef?>(null);

        public Task<IdentifierSet?> GetSiblingsBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work)
            => Task.FromResult<IdentifierSet?>(null);

        public Task<IdentifierResolution> Resolve(string identifier)
        {
            return Task.FromResult(new IdentifierResolution(identifier) { Disabled = true });
        }

        public string GetApiUrl(string canonicalId) => IdentifiersApiUrls.Canonical(baseUrl, canonicalId);

        public string GetApiUrl(string sourceSystem, string value, string type = OntologyType.Work, bool includeSiblings = false)
            => IdentifiersApiUrls.BySource(baseUrl, sourceSystem, value, type, includeSiblings);
    }
}
