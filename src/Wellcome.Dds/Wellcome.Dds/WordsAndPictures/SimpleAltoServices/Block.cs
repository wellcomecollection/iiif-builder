using System;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    [ProtoContract]
    [ProtoInclude(10, typeof(Illustration))]
    [ProtoInclude(20, typeof(TextLine))]
    public class Block
    {
        [ProtoMember(1)]
        public int Width { get; set; }
        
        [ProtoMember(2)]
        public int Height { get; set; }
        
        [ProtoMember(3)]
        public int X { get; set; }
        
        [ProtoMember(4)]
        public int Y { get; set; }
    }
}
