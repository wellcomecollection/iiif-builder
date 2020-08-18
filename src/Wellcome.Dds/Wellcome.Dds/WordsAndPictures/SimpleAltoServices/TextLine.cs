using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    public class TextLine : Block
    {
        public string Text { get; set; }
    }
}
