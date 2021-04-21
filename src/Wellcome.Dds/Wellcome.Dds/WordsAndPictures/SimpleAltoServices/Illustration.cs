using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    [ProtoContract]
    public class Illustration : Block
    {
        [ProtoMember(1)]
        public string Type { get; set; }
    }
}
