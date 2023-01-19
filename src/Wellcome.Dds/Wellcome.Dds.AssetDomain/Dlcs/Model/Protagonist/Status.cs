using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model.Protagonist;

public class Status : JSONLDBase
{
    public override string Type => "Status";

    [JsonProperty(Order = 10, PropertyName = "statusCode")]
    public int StatusCode { get; set; }

    [JsonProperty(Order = 11, PropertyName = "title")]
    public string? Title { get; set; }

    [JsonProperty(Order = 12, PropertyName = "description")]
    public string? Description { get; set; }
}