using System.Collections.Generic;

namespace Wellcome.Dds.WordsAndPictures.Search
{
    /// <summary>
    /// An individual search result
    /// </summary>
    public class SearchResult
    {
        public int Index { get; set; }
        public List<Rect>? Rects { get; set; }
    }
}
