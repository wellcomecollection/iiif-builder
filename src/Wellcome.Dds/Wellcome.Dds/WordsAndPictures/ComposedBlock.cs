using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.WordsAndPictures
{
    /// <summary>
    /// Represents an ALTO composedblock element. The most obvious use of these is MoH tables
    /// </summary>
    [Serializable]
    public class ComposedBlock
    {
        // which page is this on?
        public int ImageIndex { get; set; }

        // the index into the Words dictionary for the word with which this ComposedBlock starts. same as Word::PosNorm
        public int StartCharacter { get; set; }
        // the position of the last word in the composedBlock
        public int EndCharacter { get; set; }

        // The ALTO ID attribute of this composed block
        public string AltoId { get; set; }

        // The AltoId is only unique within a single alto file, need it to be unique across the sequence
        public string UniqueId { get; set; }

        // the ALTO "TYPE" attribute (e.g., "Table"
        public string Type { get; set; }

        // same rectangle properties as for an individual Word. These should be scaled to pixels, just as they are for words.
        public int X;
        public int Y;
        public int W;
        public int H;

        // index in sequence, not individual ALTO
        public int ComposedBlockIndex { get; set; }
    }
}
