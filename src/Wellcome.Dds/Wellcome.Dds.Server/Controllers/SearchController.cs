using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIIF.Search;
using IIIF.Serialisation;
using Microsoft.FeatureManagement.Mvc;
using Utils;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.Search;

namespace Wellcome.Dds.Server.Controllers
{
    [FeatureGate(FeatureFlags.TextServices)]
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
            string[] suggestions = new string[0];
            if (q.HasText() && q.Trim().Length > 2)
            {
                var text = await searchTextProvider.GetSearchText(manifestationIdentifier);
                if (text == null)
                {
                    return NotFound($"No text found for {manifestationIdentifier}");
                }
                suggestions = text.GetSuggestions(q);
            }
            var termList = iiifBuilder.BuildTermListV1(manifestationIdentifier, q, suggestions);
            IgnoreParams(termList);
            return Content(termList.AsJson(), "application/json");
        }

        /// <summary>
        /// http://localhost:8084/search/v0/b28047345?q=more%20robust
        /// note the v0 path. Behaves like old DDS. "Simple Search" results.
        /// </summary>
        /// <param name="manifestationIdentifier"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        [HttpGet("v0/{manifestationIdentifier}")]
        public async Task<IActionResult> SearchV0(string manifestationIdentifier, string q)
        {
            var text = await searchTextProvider.GetSearchText(manifestationIdentifier);
            if (text == null)
            {
                return NotFound($"No searchable text found for {manifestationIdentifier}");
            }
            
            IEnumerable<SearchResult> results;
            if (q.HasText())
            {
                var resultRects = text.Search(q);
                results = SearchConverter.ConvertToSimplePlayerResults(resultRects);
            }
            else
            {
                results = new SearchResult[0];
            }
            var asAnnotations = iiifBuilder.BuildSearchResultsV0(text, results, manifestationIdentifier, q);
            IgnoreParams(asAnnotations.Within);
            return Content(asAnnotations.AsJson(), "application/json");
        }

        /// <summary> 
        /// http://localhost:8084/search/v1/b28047345?q=more%20robust
        /// Hits are properly distributed.
        /// One hit can span more than one canvas.
        /// One canvas have more than one hit.
        /// </summary>
        /// <param name="manifestationIdentifier"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        [HttpGet("v1/{manifestationIdentifier}")]
        public async Task<IActionResult> SearchV1(string manifestationIdentifier, string q)
        {
            // https://github.com/wellcomecollection/platform/issues/4740#issuecomment-775035270
            var text = await searchTextProvider.GetSearchText(manifestationIdentifier);
            if (text == null)
            {
                return NotFound($"No searchable text found for {manifestationIdentifier}");
            }
            var asAnnotations = iiifBuilder.BuildSearchResultsV1(text, manifestationIdentifier, q);
            IgnoreParams(asAnnotations.Within);
            return Content(asAnnotations.AsJson(), "application/json");
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
