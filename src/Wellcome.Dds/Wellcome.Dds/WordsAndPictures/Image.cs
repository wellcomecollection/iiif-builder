using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures
{
    [Serializable]
    [ProtoContract]
    public class Image
    {
        #nullable disable
        
        // index into the Words dictionary
        [ProtoMember(1)]
        public int StartCharacter { get; set; }
        
        [ProtoMember(2)]
        public string ImageIdentifier { get; set; }
    }
}
