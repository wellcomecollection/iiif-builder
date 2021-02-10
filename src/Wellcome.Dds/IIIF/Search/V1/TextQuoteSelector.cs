﻿using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Search.V1
{
    public class TextQuoteSelector: LegacyResourceBase
    {
        public override string Type => "oa:TextQuoteSelector";

        [JsonProperty(Order = 51, PropertyName = "exact")]
        public string Exact { get; set; }

        [JsonProperty(Order = 52, PropertyName = "prefix")]
        public string Prefix { get; set; }

        [JsonProperty(Order = 53, PropertyName = "suffix")]
        public string Suffix { get; set; }
    }
}