using System;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures
{
    /// <summary>
    /// Represents an ALTO composedblock element. The most obvious use of these is MoH tables
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class ComposedBlock
    {
        // which page is this on?
        [ProtoMember(1)]
        public int ImageIndex { get; set; }

        // the index into the Words dictionary for the word with which this ComposedBlock starts. same as Word::PosNorm
        [ProtoMember(2)]
        public int StartCharacter { get; set; }
        // the position of the last word in the composedBlock
        [ProtoMember(3)]
        public int EndCharacter { get; set; }

        // index in sequence, not individual ALTO
        [ProtoMember(4)]
        public int ComposedBlockIndex { get; set; }
    }
}
