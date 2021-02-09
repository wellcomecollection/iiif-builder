using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Search.V1
{
    public class TermList : LegacyResourceBase, IHasIgnorableParameters
    {
        public override string Type => "search:TermList";

        [JsonProperty(Order = 20, PropertyName = "ignored")]
        public string[] Ignored { get; set; }

        [JsonProperty(Order = 40, PropertyName = "terms")]
        public Term[] Terms { get; set; }
    }
}