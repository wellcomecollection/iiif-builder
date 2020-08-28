using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wellcome.Dds.WordsAndPictures.TextTokens;

namespace Wellcome.Dds.WordsAndPictures.TextArtefacts
{
    public class Block
    {
        public List<Block> Blocks { get; set; }
        public List<Line> Lines { get; set; }
        
        // the index into the Words dictionary for the word with which this Block starts. same as Word::PosNorm
        // remember that this is the position in the normalised text, not the raw text.
        public int StartWordPosition { get; set; }
        // the last word in the Block
        public int EndWordPosition { get; set; }

        // the equivalent positions in the raw text. These are not dictionary keys.
        //public int StartCharacterRaw { get; set; }
        //public int EndCharacterRaw { get; set; }

        public ComposedBlock ComposedBlock { get; set; }

        // Allow users of this class, such as MoH, to add their own extra information
        public IBlockExtension Extension { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (ITextToken textToken in GetTokens())
            {
                if (textToken is BlockStartToken)
                {
                    sb.AppendLine("----------start block----------");
                }
                else if (textToken is BlockEndToken)
                {
                    sb.AppendLine("-----------end block-----------");
                }
                else
                {
                    sb.Append(textToken);
                }
            }
            return sb.ToString();
        }
        

        public IEnumerable<ITextToken> GetTokens()
        {
            int pos = -1;
            yield return new BlockStartToken {Block = this};
            if (Lines != null)
            {
                foreach (var line in Lines)
                {
                    bool first = true;
                    foreach (var word in line.Words)
                    {
                        if (Blocks != null)
                        {
                            // is there a block (or blocks) to yield before we start this word?
                            var tBlocks = Blocks.Where(b =>  b.StartWordPosition > pos &&
                                                            b.StartWordPosition < word.PosNorm);
                            foreach (var tBlock in tBlocks)
                            {
                                foreach (ITextToken textToken in tBlock.GetTokens())
                                {
                                    yield return textToken;
                                }
                            }
                        }
                        if (!first)
                        {
                            yield return SpaceToken.Instance;
                        }
                        first = false;
                        pos = word.PosNorm;
                        yield return new WordToken {Word = word};
                    }
                    yield return NewLineToken.Instance;
                }
            }
            if (Blocks != null)
            {
                // is there a block (or blocks) at the end?
                var tBlocks = Blocks.Where(b => b.StartWordPosition > pos);
                foreach (var tBlock in tBlocks)
                {
                    foreach (ITextToken textToken in tBlock.GetTokens())
                    {
                        yield return textToken;
                    }
                }
            }
            yield return new BlockEndToken { Block = this };
        }
    }
}
