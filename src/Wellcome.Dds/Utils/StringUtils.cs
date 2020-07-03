using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public static DateTime? GetNullableDateTime(string s)
        {
            DateTime date;
            if (DateTime.TryParse(s, out date))
            {
                return date;
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// Strips out any character that is not a letter or a digit.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToAlphanumeric(this string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Splits a string into an array of strings using the supplied delimiter.
        /// Each string in the returned sequence is TRIMMED.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public static string[] SplitByDelimiterIntoArray(this string source, char delimiter)
        {
            var strings = SplitByDelimiter(source, delimiter);
            if (strings == null)
            {
                return new string[0];
            }
            return strings.ToArray();
        }

        /// <summary>
        /// Splits a string into a sequence of strings using the supplied delimiter.
        /// Each string in the returned sequence is TRIMMED.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public static IEnumerable<string> SplitByDelimiter(this string source, char delimiter)
        {
            if (!String.IsNullOrEmpty(source))
            {
                // this trims whitespace by default - implement another one if required
                var strings = source.Split(new[] { delimiter });
                return strings.Where(s => s.HasText()).Select(s => s.Trim());
            }
            return null;
        }


        /// <summary>
        /// Removes separator from the end of str if it's there, otherwise leave it alone.
        /// 
        /// "something/", "/" => "something"
        /// "something", "thing" => "some"
        /// "something", "some" => "something"
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string Chomp(this string str, string separator)
        {
            if (str == null) return null;
            if (str == String.Empty) return String.Empty;
            if (str.EndsWith(separator))
            {
                return str.Substring(0, str.LastIndexOf(separator, StringComparison.Ordinal));
            }
            return str;
        }

        public static string NormaliseSpaces(this string s)
        {
            if (s.HasText())
            {
                while (s.IndexOf("  ", StringComparison.Ordinal) != -1)
                {
                    s = s.Replace("  ", " ");
                }
                s = s.Trim();
            }
            return s;
        }
    }
}