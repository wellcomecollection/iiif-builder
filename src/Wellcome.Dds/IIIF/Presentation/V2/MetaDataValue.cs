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
            // "none" is used in P3 if language unknown
            const string unknownLanguage = "none";
            if (languageMap == null) return null;
            
            var langVals = new List<LanguageValue>();
            foreach (var kvp in languageMap)
            {
                var language = kvp.Key == unknownLanguage ? null : kvp.Key;
                langVals.AddRange(kvp.Value.Select(values => new LanguageValue
                {
                    Language = language,
                    Value = values
                }));
            }

            return new MetaDataValue(langVals);
        }

        public static MetaDataValue? Create(LabelValuePair? labelValuePair)
        {
            if (labelValuePair == null) return null;

            // TODO - what is correct here?
            return Create(labelValuePair.Value);
        }
    }
}