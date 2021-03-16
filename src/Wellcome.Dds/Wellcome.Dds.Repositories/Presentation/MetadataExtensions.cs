using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation.V3.Strings;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class MetadataExtensions
    {
        public static void AddNonlang(this List<LabelValuePair> md, string label, IEnumerable<string> values)
        {
            md.Add(label, values.ToList(), "none");
        }
        
        public static void AddEnglish(this List<LabelValuePair> md, string label, IEnumerable<string> values)
        {
            md.Add(label, values.ToList(), "en");
        }

        public static void AddEnglish(this List<LabelValuePair> md, string label, string value)
        {
            md.Add(label, value, "en");
        }
        
        public static void AddNonlang(this List<LabelValuePair> md, string label, string value)
        {
            md.Add(label, value, "none");
        }

        public static void Add(this List<LabelValuePair> md, string label, string value, string valueLanguage)
        {
            if (value.HasText())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, value);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }
        
        
        public static void Add(this List<LabelValuePair> md, string label, IList<string> values, string valueLanguage)
        {
            if (values.Any())
            {
                var labelMap = new LanguageMap("en", label);
                var valueMap = new LanguageMap(valueLanguage, values);
                md.Add(new LabelValuePair(labelMap, valueMap));
            }
        }
        
    }
}