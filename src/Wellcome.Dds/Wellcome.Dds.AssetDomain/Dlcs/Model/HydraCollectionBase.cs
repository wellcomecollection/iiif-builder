using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class HydraCollectionBase : JSONLDBase
    {
        public override string Context => "http://www.w3.org/ns/hydra/context.jsonld";
        public override string Type => "Collection";

        [JsonProperty(Order = 10, PropertyName = "totalItems")]
        public int? TotalItems { get; set; }

        [JsonProperty(Order = 11, PropertyName = "pageSize")]
        public int? PageSize { get; set; }

        [JsonProperty(Order = 90, PropertyName = "view")]
        public PartialCollectionView? View { get; set; }
    }
}
