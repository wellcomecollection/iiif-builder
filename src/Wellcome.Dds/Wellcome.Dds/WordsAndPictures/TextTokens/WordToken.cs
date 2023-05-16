namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class WordToken : ITextToken
    {
        public WordToken(Word word)
        {
            Word = word;
        }

        public Word Word { get; set; }

        public override string ToString()
        {
            return Word.ToRawString();
        }
    }
}
