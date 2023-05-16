using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class HydraErrorByMetadataCollection : HydraCollectionBase
    {
        [JsonProperty(Order = 20, PropertyName = "member")]
        public ErrorByMetadata[]? Members { get; set; }
    }
}
