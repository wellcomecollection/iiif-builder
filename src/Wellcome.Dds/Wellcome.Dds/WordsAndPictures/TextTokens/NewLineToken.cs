using System;

namespace Wellcome.Dds.WordsAndPictures.TextTokens
{
    public class NewLineToken : ITextToken
    {
        public static NewLineToken Instance { get; set; }
        static NewLineToken() { Instance = new NewLineToken();}

        public override string ToString()
        {
            return Environment.NewLine;
        }
    }
}
