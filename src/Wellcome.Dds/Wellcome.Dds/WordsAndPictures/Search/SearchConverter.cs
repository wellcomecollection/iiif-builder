using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.WordsAndPictures.Search
{
    public class SearchConverter
    {
        /// <summary>
        /// Convert the results to the simplified player format, just page indexes and rectangles
        /// </summary>
        public static IEnumerable<SearchResult> ConvertToSimplePlayerResults(List<ResultRect> results)
        {
            var playerResults = results
                .GroupBy(rr => rr.Idx)
                .Select(gp => new SearchResult
                {
                    Index = gp.Key,
                    Rects = gp.Select(rr => new Rect
                    {
                        X = rr.X,
                        Y = rr.Y,
                        W = rr.W,
                        H = rr.H,
                        Hit = rr.Hit,
                        Before = rr.Before,
                        After = rr.After,
                        Word = rr.ContentRaw
                    }).ToList()
                });
            return playerResults;
        }
    }
}
