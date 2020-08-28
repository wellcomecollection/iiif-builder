namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class WordToken : ITextToken
    {
        public Word Word { get; set; }

        public override string ToString()
        {
            return Word.ToRawString();
        }
    }
}
