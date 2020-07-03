using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class Id
    {
        [JsonProperty(Order = 10, PropertyName = "id")]
        public string Value{ get; set; }
        
        public Id(string id)
        {
            Value = id;
        }
    }
}
