using System.Linq;
using IIIF.Presentation.V3.Strings;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class Lang
    {
        public static LanguageMap Map(string value) => new ("en", value.Trim());

        public static LanguageMap Map(string lang, string value) => new (lang, value.Trim());

        public static LanguageMap Map(string lang, params string[] values)
        {
            var map = new LanguageMap(lang, values[0].Trim());
            map[lang].AddRange(values.Skip(1).Select(v => v.Trim()));
            return map;
        }
    }
}