using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.WordsAndPictures.TextArtefacts
{
    public class Line
    {
        public int LineNumber { get; set; }
        public List<Word>? Words { get; set; }

        public string RawText()
        {
            if (Words == null || !Words.Any())
            {
                return String.Empty;
            }
            return String.Join(" ", Words.Select(w => w.ToRawString()));
        }

        public override string ToString()
        {
            const string template = "[({0}) {1}]";
            const string empty = "<no words>";
            string line;
            if (Words == null || !Words.Any())
            {
                line = empty;
            }
            else
            {
                line = String.Join(" ", Words.Select(w => w.ToString()));
            }
            return String.Format(template, LineNumber, line);
        }
    }
}
