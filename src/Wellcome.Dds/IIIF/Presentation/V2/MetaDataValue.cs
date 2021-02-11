using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V2.Serialisation;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    [JsonConverter(typeof(MetaDataValueSerialiser))]
    public class MetaDataValue
    {
        public LanguageValue[] LanguageValues { get; set; }

        public MetaDataValue(string value)
        {
            LanguageValues = new[] { new LanguageValue { Value = value } };
        }

        public MetaDataValue(string value, string language)
        {
            LanguageValues = new[] { new LanguageValue { Value = value, Language = language } };
        }

        public MetaDataValue(IEnumerable<LanguageValue> languageValues)
        {
            LanguageValues = languageValues.ToArray();
        }
    }
}