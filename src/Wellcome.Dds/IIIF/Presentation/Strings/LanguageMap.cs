using System.Collections.Generic;

namespace IIIF.Presentation.Strings
{
    public class LanguageMap : Dictionary<string, List<string>>
    {
        public LanguageMap() : base() { }

        // is this a dict?
        public LanguageMap(string language, string singleValue)
        {
            this[language] = new List<string> { singleValue };
        }
    }
}
