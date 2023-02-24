using Wellcome.Dds.WordsAndPictures.TextArtefacts;

namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class BlockEndToken : ITextToken
    {
        public BlockEndToken(Block block)
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
