using System;
using System.Collections.Generic;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures.SimpleAltoServices
{
    [Serializable]
    [ProtoContract]
    public class AnnotationPage
    {
        [ProtoMember(1)]
        public int Index { get; set; }
        
        [ProtoMember(2)]
        public TextLine[] TextLines { get; set; }
        
        [ProtoMember(3)]
        public Illustration[] Illustrations { get; set; }
        
        [ProtoMember(4)]
        public Illustration[] ComposedBlocks { get; set; }

        public override string ToString() => $"Index {Index}; {TextLines?.Length ?? 0} lines";

        [ProtoMember(5)]
        public string ManifestationIdentifier { get; set; }
        
        [ProtoMember(6)]
        public string AssetIdentifier { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public class AnnotationPageList : List<AnnotationPage> { }
}
