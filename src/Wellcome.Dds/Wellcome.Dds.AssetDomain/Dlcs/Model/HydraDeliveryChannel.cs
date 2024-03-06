using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model;

public class HydraDeliveryChannel
{
    [JsonProperty(Order = 10, PropertyName = "channel")]
    public string? Channel { get; set; }
    
    [JsonProperty(Order = 20, PropertyName = "policy")]
    public string? Policy { get; set; }

    public override string ToString()
    {
        return $"{Channel ?? "(no channel)"}|{Policy ?? "(no policy)"}";
    }
}