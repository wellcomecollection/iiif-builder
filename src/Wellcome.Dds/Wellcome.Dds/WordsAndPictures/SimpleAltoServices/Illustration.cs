using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    public class Illustration : Block
    {
        public string Type { get; set; }
    }
}
