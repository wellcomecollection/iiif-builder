using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V2.Serialisation;
using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    [JsonConverter(typeof(MetaDataValueSerialiser))]
    public class MetaDataValue
    {
        public List<LanguageValue> LanguageValues { get; set; }

        public MetaDataValue(string value) 
            => LanguageValues = new List<LanguageValue> {new() {Value = value}};

        public MetaDataValue(string value, string language) 
            => LanguageValues = new List<LanguageValue> {new() {Value = value, Language = language}};

        public MetaDataValue(IEnumerable<LanguageValue> languageValues) 
            => LanguageValues = languageValues.ToList();

        public MetaDataValue(LanguageMap languageMap)
        {
            List<LanguageValue> langVals = new();
            foreach (var kvp in languageMap)
            {
                langVals.AddRange(kvp.Value.Select(values => new LanguageValue
                {
                    Language = kvp.Key,
                    Value = values
                }));
            }

            LanguageValues = langVals;
        }
    }
}