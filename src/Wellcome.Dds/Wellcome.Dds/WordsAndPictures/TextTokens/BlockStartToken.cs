﻿using Wellcome.Dds.WordsAndPictures.TextArtefacts;

namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class BlockStartToken : ITextToken
    {
        public BlockStartToken(Block block)
        {
            Block = block;
        }

        public Block Block { get; set; }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
