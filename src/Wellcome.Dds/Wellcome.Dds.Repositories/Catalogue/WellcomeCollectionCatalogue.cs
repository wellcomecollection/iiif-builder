using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Catalogue
{
    public class WellcomeCollectionCatalogue : ICatalogue
    {
        private ILogger<WellcomeCollectionCatalogue> logger;
        private DdsOptions options;
        private readonly HttpClient httpClient;

        private readonly string[] allIncludes = new[] { 
            "identifiers",
            "items",
            "subjects",
            "genres",
            "contributors",
            "production",
            "notes"
        };

        public WellcomeCollectionCatalogue(
            ILogger<WellcomeCollectionCatalogue> logger,
            IOptions<DdsOptions> ddsOptions,
            HttpClient httpClient)
        {
            this.logger = logger;
            options = ddsOptions.Value;
            this.httpClient = httpClient;
        }

        public async Task<Work> GetWork(string identifier)
        {
            var resultPage = await GetWorkResultPage(null, identifier);
            if(resultPage.Results.HasItems())
            {
                if(resultPage.Results.Length == 1)
                {
                    // easy, nothing to decide between
                    return resultPage.Results[0];
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
                return matchedWork;
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
            string queryString = BuildQueryString(query, identifiers, include, pageSize);
            var url = options.ApiWorkTemplate + queryString;
            var response = await httpClient.GetAsync(url);
            return await response.Content.ReadFromJsonAsync<WorkResultPage>();
        }

        private string BuildQueryString(string query, string identifiers, IEnumerable<string> include, int pageSize)
        {
            var args = new List<string>();
            if (query.HasText())
            {
                args.Add($"query={query}");
            }
            if (identifiers.HasText())
            {
                args.Add($"identifiers={identifiers}");
            }
            if (include == null)
            {
                include = allIncludes;
            }
            if (include.HasItems())
            {
                args.Add($"include={string.Join(',', include)}");
            }
            if (pageSize > 0)
            {
                args.Add($"pageSize={pageSize}");
            }
            var queryString = "?" + string.Join('&', args);
            return queryString;
        }

    }
}
