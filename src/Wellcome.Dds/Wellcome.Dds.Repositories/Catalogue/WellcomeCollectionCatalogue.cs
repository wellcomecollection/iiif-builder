using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Catalogue
{
    public class WellcomeCollectionCatalogue : ICatalogue
    {
        private readonly DdsOptions options;
        private readonly HttpClient httpClient;

        /// <summary>
        /// include=identifiers,items,subjects,genres,contributors,production,notes,parts,partOf,precededBy,succeededBy
        /// </summary>
        private readonly string[] allIncludes = { 
            "identifiers",
            "items",
            "subjects",
            "genres",
            "contributors",
            "production",
            "notes",
            "parts",
            "partOf",
            "precededBy",
            "succeededBy"
        };

        public WellcomeCollectionCatalogue(
            IOptions<DdsOptions> ddsOptions,
            HttpClient httpClient)
        {
            options = ddsOptions.Value;
            this.httpClient = httpClient;
        }

        public async Task<Work> GetWorkByOtherIdentifier(string identifier)
        {
            var resultPage = await GetWorkResultPage(null, identifier, null, 0);
            if(resultPage.Results.HasItems())
            {
                if(resultPage.Results.Length == 1)
                {
                    // easy, nothing to decide between
                    // see https://digirati.slack.com/archives/CBT40CMKQ/p1597936607018500
                    // We need to obtain the work by its WorkID to make sure we're not missing anything
                    // return resultPage.Results[0];
                    return await GetWorkByWorkId(resultPage.Results[0].Id);
                }

                // TODO - handle paging, if there is a nextPage in this result set
                // The API can return more than one work for a given identifier.
                // See b14658197
                Work matchedWork = null;
                List<Work> relatedWorks = new List<Work>();
                foreach(var work in resultPage.Results)
                {
                    if(IsMatchedWork(work))
                    {
                        if(matchedWork == null)
                        {
                            matchedWork = work;
                        }
                        else
                        {
                            throw new NotSupportedException("Can't match more than one work!");
                        }
                    }
                    else
                    {
                        relatedWorks.Add(work);
                    }
                }
                if(matchedWork != null)
                {
                    matchedWork.RelatedByIdentifier = relatedWorks.ToArray();
                }
                return await GetWorkByWorkId(matchedWork.Id);
            }
            return null;
        }


        /// <summary>
        /// This is NOT the real implementation yet! Need to try it on all the bnumbers and build these rules out.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        private bool IsMatchedWork(Work work)
        {
            if(work.WorkType.Id == "k")
            {
                return true;
            }
            return false;
        }

        public Task<WorkResultPage> GetWorkResultPage(string query, string identifiers)
        {
            return GetWorkResultPage(query, identifiers, null, 0);
        }

        public async Task<WorkResultPage> GetWorkResultPage(string query, string identifiers, IEnumerable<string> include, int pageSize)
        {
            var queryString = BuildQueryString(query, identifiers, include, pageSize);
            var url = options.ApiWorkTemplate + queryString;
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WorkResultPage>();
        }

        public string GetCatalogueApiUrl(string workId, string[] include = null)
        {
            var queryString = BuildQueryString(null, null, include, -1);
            return $"{options.ApiWorkTemplate}/{workId}{queryString}";
        }

        public async Task<Work> GetWorkByWorkId(string workId)
        {
            var url = GetCatalogueApiUrl(workId);
            var response = await httpClient.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<Work>();
        }

        private string BuildQueryString(string query, string identifiers, IEnumerable<string> include, int pageSize)
        {
            var args = new List<string>();
            if (include == null)
            {
                include = allIncludes;
            }
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
