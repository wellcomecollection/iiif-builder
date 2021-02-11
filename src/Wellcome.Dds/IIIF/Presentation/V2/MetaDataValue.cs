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

        /// <summary>
        /// Create a new MetaDataValue object from specified LanguageMap.
        /// </summary>
        /// <returns>Null if LanguageMap null, else new MetaDataValue object </returns>
        public static MetaDataValue? Create(LanguageMap? languageMap)
        {
            if (languageMap == null) return null;
            
            var langVals = new List<LanguageValue>();
            foreach (var kvp in languageMap)
            {
                langVals.AddRange(kvp.Value.Select(values => new LanguageValue
                {
                    Language = kvp.Key,
                    Value = values
                }));
            }

            return new MetaDataValue(langVals);
        }
    }
}