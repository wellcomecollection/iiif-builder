using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.WordsAndPictures
{
    [Serializable]
    public class Image
    {
        public string OrderLabel { get; set; }
        public int Index { get; set; }

        // index into the Words dictionary
        public int StartCharacter { get; set; }
        //public int EndCharacter { get; set; }
        public string ImageIdentifier { get; set; }
    }
}
