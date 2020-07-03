using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class HydraImageCollection : HydraCollectionBase
    {
        [JsonProperty(Order = 20, PropertyName = "member")] 
        public Image[] Members { get; set; }
    }
}
