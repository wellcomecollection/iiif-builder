using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    public class LanguageValue : ValueObject
    {
        //@language
        [JsonProperty(Order = 4, PropertyName = "@language")]
        public string Language { get; set; }

    }
}