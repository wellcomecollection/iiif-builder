using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace IIIF.Search.V1
{
    public class SearchService : LegacyResourceBase, IService
    {
        public const string Search1Context = "http://iiif.io/api/search/1/context.json";
        public const string Search1Profile = "http://iiif.io/api/search/1/search";

        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string Type => nameof(SearchService);

        

        public AutoCompleteService? Service { get; set; }
    }
}
