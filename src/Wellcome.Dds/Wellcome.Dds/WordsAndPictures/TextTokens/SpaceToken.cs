namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class SpaceToken : ITextToken
    {
        public static SpaceToken Instance { get; set; }
        static SpaceToken() { Instance = new SpaceToken(); }

        public override string ToString()
        {
            return " ";
        }
    }
}
