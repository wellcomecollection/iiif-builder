namespace Utils
{
    public static class StringUtils
    {
        /// <summary>
        /// Does this string have significant content (is not null, empty, or just whitespace character(s))
        /// </summary>
        /// <remarks>
        /// This may seem trivial but it helps code readability.
        /// </remarks>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool HasText(this string s) => !string.IsNullOrWhiteSpace(s);

        /// <summary> 
        /// Removes separator from the start of str if it's there, otherwise leave it alone.
        /// 
        /// "something", "thing" => "something"
        /// "something", "some" => "thing"
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static string RemoveStart(this string str, string start)
        {
            if (str == null) return null;
            if (str == string.Empty) return string.Empty;

            if (str.StartsWith(start) && str.Length > start.Length)
            {
                return str.Substring(start.Length);
            }

            return str;
        }
    }
}