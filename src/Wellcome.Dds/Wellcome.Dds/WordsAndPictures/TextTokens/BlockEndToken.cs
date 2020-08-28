using Wellcome.Dds.WordsAndPictures.TextArtefacts;

namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class BlockEndToken : ITextToken
    {
        public Block Block { get; set; }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
