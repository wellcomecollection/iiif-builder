using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Catalogue
{
    public class WellcomeCollectionCatalogue : ICatalogue
    {
        private readonly DdsOptions options;
        private readonly HttpClient httpClient;
        private readonly ILogger<WellcomeCollectionCatalogue> logger;

        /// <summary>
        /// include=identifiers,items,subjects,genres,contributors,production,languages,notes,images,parts,partOf,precededBy,succeededBy
        /// </summary>
        private readonly string[] allIncludes = { 
            "identifiers",
            "items",
            "subjects",
            "genres",
            "contributors",
            "production",
            "languages",
            "notes",
            "images",
            "parts",
            "partOf",
            "precededBy",
            "succeededBy"
        };

        public WellcomeCollectionCatalogue(
            IOptions<DdsOptions> ddsOptions,
            HttpClient httpClient,
            ILogger<WellcomeCollectionCatalogue> logger)
        {
            options = ddsOptions.Value;
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<Work?> GetWorkByOtherIdentifier(string identifier)
        {
            return (await FindWorksByOtherIdentifier(identifier)).Chosen;
        }

        public async Task<WorkSelection> FindWorksByOtherIdentifier(string identifier)
        {
            // TODO - handle paging, if there is a nextPage in this result set (pageSize defaults to 100)
            var resultPage = await GetWorkResultPage(null, identifier, null, 0);
            var selection = WorkSelection.Choose(identifier, resultPage?.Results);
            if (selection.Chosen == null)
            {
                if (selection.Ambiguous)
                {
                    // Callers treat null as "no catalogue work" and recover on the next pass; a guess would persist.
                    logger.LogWarning("Identifiers query for '{Identifier}' is ambiguous: {Reason}", identifier.LogSafe(), selection.Reason);
                }
                return selection;
            }
            if (selection.Candidates.Count > 1)
            {
                logger.LogInformation("Identifiers query for '{Identifier}': {Reason}", identifier.LogSafe(), selection.Reason);
            }

            // The search response is a cut-down work: refetch the chosen one by id with the full include list.
            // See https://digirati.slack.com/archives/CBT40CMKQ/p1597936607018500
            var others = selection.Others.ToArray();
            selection.Chosen = await GetWorkByWorkId(selection.Chosen.Id!);
            if (selection.Chosen != null && others.Length > 0)
            {
                selection.Chosen.RelatedByIdentifier = others;
            }
            return selection;
        }

        public Task<WorkResultPage?> GetWorkResultPage(string? query, string identifiers)
        {
            return GetWorkResultPage(query, identifiers, null, 0);
        }

        public async Task<WorkResultPage?> GetWorkResultPage(string? query, string identifiers, IEnumerable<string>? include, int pageSize)
        {
            var queryString = BuildQueryString(query, identifiers, include, pageSize);
            var url = options.ApiWorkTemplate + queryString;
            var response = await MakeRequest(url);
            var wrp = await response.Content.ReadFromJsonAsync<WorkResultPage>();
            return wrp;
        }

        public string GetCatalogueApiUrl(string workId, string[]? include = null)
        {
            var queryString = BuildQueryString(null, null, include, -1);
            return $"{options.ApiWorkTemplate}/{workId}{queryString}";
        }

        public async Task<Work?> GetWorkByWorkId(string workId)
        {
            var url = GetCatalogueApiUrl(workId);
            var response = await MakeRequest(url);
            return await response.Content.ReadFromJsonAsync<Work>();
        }
        
        private async Task<HttpResponseMessage> MakeRequest(string url)
        {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Request to '{url}' returned {statusCode}", url.LogSafe(), response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        public Work? FromDumpLine(string line, JsonSerializerOptions? serializerOptions)
        {
            return JsonSerializer.Deserialize<Work>(line, serializerOptions);
        }

        private string BuildQueryString(string? query, string? identifiers, IEnumerable<string>? include, int pageSize)
        {
            var args = new List<string>();
            include ??= allIncludes;
            
            var includes = include.ToArray();
            if (query.HasText())
            {
                args.Add($"query={query}");
            }
            if (identifiers.HasText())
            {
                args.Add($"identifiers={identifiers}");
            }
            if (includes.Length > 0)
            {
                args.Add($"include={string.Join(',', includes)}");
            }
            if (pageSize == 0)
            {
                // default to 100, rather than the API default of 10
                pageSize = 100;
            }
            if (pageSize > 0)
            {
                args.Add($"pageSize={pageSize}");
            }
            if (args.Any())
            {
                return "?" + string.Join('&', args);
            }
            return string.Empty;
        }
    }
}
