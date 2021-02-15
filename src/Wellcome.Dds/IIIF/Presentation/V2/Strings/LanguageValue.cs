using Newtonsoft.Json;

namespace IIIF.Presentation.V2.Strings
{
    public class LanguageValue : ValueObject
    {
        [JsonProperty(Order = 4, PropertyName = "@language")]
        public string? Language { get; set; }

    }
}