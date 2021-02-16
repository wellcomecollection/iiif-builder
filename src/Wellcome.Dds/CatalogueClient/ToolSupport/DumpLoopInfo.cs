using System.Collections.Generic;

namespace CatalogueClient.ToolSupport
{
    public class DumpLoopInfo
    {
        public string Filter;
        public int Skip = 1;
        public int TotalCount;
        public int MatchCount;
        public int UsedLines;
        public readonly HashSet<string> UniqueDigitisedBNumbers = new();
        public readonly HashSet<string> BNumbersInMoreThanOneLine = new();
        
        public const string IIIFLocationFilter = "\"iiif-presentation\"";
    }
}