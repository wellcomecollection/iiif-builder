﻿using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Search.V1
{
    public class SearchService1 : LegacyResourceBase, IService
    {
        public const string Search1Context = "http://iiif.io/api/search/1/context.json";
        public const string Search1Profile = "http://iiif.io/api/search/1/search";

        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string Type => nameof(SearchService1);

        

        public AutoCompleteService1? Service { get; set; }
    }
}