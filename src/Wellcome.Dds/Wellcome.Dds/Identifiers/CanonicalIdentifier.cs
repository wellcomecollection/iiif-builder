using System.Text.RegularExpressions;

namespace Wellcome.Dds.Identifiers
{
    public static class CanonicalIdentifier
    {
        /// <summary>
        /// 8 character public catalogue id. Alphabet a-z and 2-9, excluding o/i/l/1, first character a letter.
        /// This is the pattern the API Gateway enforces; anything else is a 400 before it reaches the registry.
        /// </summary>
        public static readonly Regex Pattern = new Regex("^[a-hjkmnp-z][a-hjkmnp-z2-9]{7}$");

        public static bool IsValid(string? s)
        {
            return s != null && Pattern.IsMatch(s);
        }
    }
}
