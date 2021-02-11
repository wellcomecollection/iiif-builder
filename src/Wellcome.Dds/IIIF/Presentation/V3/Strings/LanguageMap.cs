using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IIIF.Presentation.V3.Strings
{
    public class LanguageMap : Dictionary<string, List<string>>
    {
        public LanguageMap() : base() { }

        // is this a dict?
        public LanguageMap(string language, string singleValue)
        {
            this[language] = new List<string> { singleValue };
        }
        
        public LanguageMap(string language, IEnumerable<string> values)
        {
            this[language] = values.ToList();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (List<string> value in Values)
            {
                foreach (string s in value)
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }
                    sb.Append(s);
                }
            }
            return sb.ToString();
        }
    }
}
