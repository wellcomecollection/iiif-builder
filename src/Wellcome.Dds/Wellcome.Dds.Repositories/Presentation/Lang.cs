using System.Linq;
using IIIF.Presentation.Strings;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class Lang
    {
        
        public static LanguageMap Map(string value)
        {
            return new LanguageMap("en", value);
        }
        
        public static LanguageMap Map(string lang, string value)
        {
            return new LanguageMap(lang, value);
        }

        public static LanguageMap Map(string lang, params string[] values)
        {
            var map = new LanguageMap(lang, values[0]);
            map[lang].AddRange(values.Skip(1));
            return map;
        }
    }
}