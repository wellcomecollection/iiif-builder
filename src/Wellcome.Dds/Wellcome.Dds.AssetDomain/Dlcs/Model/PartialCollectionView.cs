using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class PartialCollectionView : JSONLDBase
    {
        public override string Context
        {
            get { return "http://www.w3.org/ns/hydra/context.jsonld"; }
        }

        public override string Type
        {
            get { return "PartialCollectionView"; }
        }

        [JsonProperty(Order = 11, PropertyName = "first")]
        public string First { get; set; }

        [JsonProperty(Order = 12, PropertyName = "previous")]
        public string Previous { get; set; }

        [JsonProperty(Order = 13, PropertyName = "next")]
        public string Next { get; set; }

        [JsonProperty(Order = 14, PropertyName = "last")]
        public string Last { get; set; }
    }
}
