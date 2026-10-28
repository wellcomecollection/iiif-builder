using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Repositories.Identifiers
{
    /// <summary>
    /// HTTP client for the Identifiers API (RFC 089). Register with AddHttpClient&lt;IIdentifiersApi, WellcomeCollectionIdentifiersApi&gt;().
    /// </summary>
    public class WellcomeCollectionIdentifiersApi : IIdentifiersApi
    {
        private const string ApiKeyHeader = "x-api-key";
        private const string IncludeSiblings = "siblings";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private readonly IdentifiersApiOptions options;
        private readonly HttpClient httpClient;
        private readonly ILogger<WellcomeCollectionIdentifiersApi> logger;
        private readonly string baseUrl;

        public WellcomeCollectionIdentifiersApi(
            IOptions<IdentifiersApiOptions> identifiersApiOptions,
            HttpClient httpClient,
            ILogger<WellcomeCollectionIdentifiersApi> logger)
        {
            options = identifiersApiOptions.Value;
            this.httpClient = httpClient;
            this.logger = logger;
            baseUrl = options.BaseUrl.TrimEnd('/');
        }

        public async Task<IdentifierSet?> GetByCanonicalId(string canonicalId)
        {
            return await Get<IdentifierSet>(GetApiUrl(canonicalId));
        }

        public async Task<CanonicalIdRef?> GetBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work)
        {
            return await Get<CanonicalIdRef>(GetApiUrl(sourceSystem, value, type, false));
        }

        public async Task<IdentifierSet?> GetSiblingsBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work)
        {
            return await Get<IdentifierSet>(GetApiUrl(sourceSystem, value, type, true));
        }

        public async Task<IdentifierResolution> Resolve(string identifier)
        {
            var resolution = new IdentifierResolution(identifier);
            foreach (var candidate in IdentifierCandidates.GetCandidates(identifier))
            {
                var apiUrl = candidate.IsCanonical
                    ? GetApiUrl(candidate.Value)
                    : GetApiUrl(candidate.SourceSystem!, candidate.Value, candidate.Type, true);
                var lookup = new CandidateLookup(candidate, apiUrl);
                try
                {
                    lookup.IdentifierSet = await Get<IdentifierSet>(apiUrl);
                }
                catch (IdentifiersApiException ex)
                {
                    lookup.Error = ex.Message;
                }
                resolution.Lookups.Add(lookup);
            }
            return resolution;
        }

        public string GetApiUrl(string canonicalId)
        {
            return $"{baseUrl}/{Uri.EscapeDataString(canonicalId.Trim())}";
        }

        public string GetApiUrl(string sourceSystem, string value, string type = OntologyType.Work, bool includeSiblings = false)
        {
            // value must be a single path segment, so a CALM ref like PPCRI/A/1 is sent as PPCRI%2FA%2F1
            var url = $"{baseUrl}/by-source/{Uri.EscapeDataString(sourceSystem.Trim())}/{Uri.EscapeDataString(value.Trim())}";
            var args = new List<string>();
            if (type.HasText())
            {
                args.Add($"type={Uri.EscapeDataString(type.Trim())}");
            }
            if (includeSiblings)
            {
                args.Add($"include={IncludeSiblings}");
            }
            return args.Count > 0 ? url + "?" + string.Join('&', args) : url;
        }

        /// <summary>
        /// GET the url, returning null for a 404 and throwing for any other non-success status.
        /// </summary>
        private async Task<T?> Get<T>(string url) where T : class
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (options.ApiKey.HasText())
            {
                request.Headers.Add(ApiKeyHeader, options.ApiKey);
            }
            else
            {
                logger.LogWarning("No API key configured for the Identifiers API; request to '{url}' will be rejected", url.LogSafe());
            }

            var response = await httpClient.SendAsync(request);
            // Read as a string rather than ReadFromJsonAsync so we are not dependent on the Content-Type header
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("Identifiers API has no mapping for '{url}'", url.LogSafe());
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = TryParseError(body);
                logger.LogWarning("Request to '{url}' returned {statusCode}: {error}", url.LogSafe(), response.StatusCode, error?.Message);
                throw new IdentifiersApiException(url, response.StatusCode, error);
            }

            var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
            if (result is IdentifierSet identifierSet)
            {
                identifierSet.ETag = response.Headers.ETag?.ToString();
                identifierSet.CacheControl = response.Headers.CacheControl?.ToString();
            }
            return result;
        }

        private static IdentifiersApiError? TryParseError(string body)
        {
            if (!body.HasText())
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<IdentifiersApiError>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return new IdentifiersApiError { Message = body };
            }
        }
    }
}
