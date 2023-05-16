using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{    
    [Serializable]
    [ProtoContract]
    public class TextLine : Block
    {
        #nullable disable
        
        [ProtoMember(1)]
        public string Text { get; set; }
    }
}
