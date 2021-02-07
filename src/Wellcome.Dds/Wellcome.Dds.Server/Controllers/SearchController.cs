using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Search;
using Utils;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.Search;

namespace Wellcome.Dds.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private CachingAltoSearchTextProvider searchTextProvider;
        private IIIIFBuilder iiifBuilder;

        private static readonly string[] SearchParameters = { "motivation", "date", "user", "box" };

        public SearchController(
            CachingAltoSearchTextProvider searchTextProvider,
            IIIIFBuilder iiifBuilder
            )
        {
            this.searchTextProvider = searchTextProvider;
            this.iiifBuilder = iiifBuilder;
        }

        [HttpGet("autocomplete/v1/{manifestationIdentifier}")]
        public async Task<IActionResult> AutoCompleteV1(string manifestationIdentifier, string q)
        {
            var text = await searchTextProvider.GetSearchText(manifestationIdentifier);
            var suggestions = text.GetSuggestions(q);
            var termList = iiifBuilder.BuildTermListV1(manifestationIdentifier, q, suggestions);
            IgnoreParams(termList);
            return Content(iiifBuilder.Serialise(termList), "application/json");
        }


        [HttpGet("v1/{manifestationIdentifier}")]
        public async Task<IActionResult> SearchV1(string manifestationIdentifier, string q)
        {
            var text = await searchTextProvider.GetSearchText(manifestationIdentifier);
            IEnumerable<SearchResult> results;
            if (q.HasText())
            {
                results = SearchConverter.ConvertToSimplePlayerResults(text.Search(q));
            }
            else
            {
                results = new SearchResult[0];
            }
            var asAnnotations = iiifBuilder.BuildSearchResultsV1(results, manifestationIdentifier, q);
            IgnoreParams(asAnnotations.Within);
            // this implementation ignores all the other params
            return Content(iiifBuilder.Serialise(asAnnotations), "application/json");
        }

        private void IgnoreParams(IHasIgnorableParameters resource)
        {
            var ignored = GetIgnoredParameters();
            if (ignored.Any())
            {
                resource.Ignored = ignored;
            }
        }

        /// <summary>
        /// Request parameters for search that IIIF-Builder does not process
        /// </summary>
        /// <returns></returns>
        private string[] GetIgnoredParameters()
        {
            var ignored = new List<string>();
            foreach (string key in Request.Query.Keys)
            {
                var value = Request.Query[key][0];
                if (SearchParameters.Contains(key) && !string.IsNullOrWhiteSpace(value))
                {
                    ignored.Add(key);
                }
            }
            return ignored.ToArray();
        }
    }
}
