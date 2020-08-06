using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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


        /// <summary>
        /// Create a nice display format for file size given a raw byte value
        /// 
        /// 42 => "42 B"
        /// 1100 => "1.07 KB"
        /// 6958472 => "6.37 MB"
        /// 
        /// </summary>
        /// <param name="sizeInBytes"></param>
        /// <returns></returns>
        public static string FormatFileSize(long sizeInBytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (sizeInBytes == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(sizeInBytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(sizeInBytes) * num) + suf[place];
        }


        /// <summary>
        /// like String.Replace, but only replaces the first instance of search in str
        /// </summary>
        /// <param name="str"></param>
        /// <param name="search"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        public static string ReplaceFirst(this string str, string search, string replace)
        {
            if (String.IsNullOrEmpty(search))
            {
                return str;
            }
            int pos = str.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return str;
            }
            return str.Substring(0, pos) + replace + str.Substring(pos + search.Length);
        }


        public static string GetFriendlyAge(DateTime? dtn)
        {
            if (dtn.HasValue)
            {
                return GetFriendlyAge(dtn.Value);
            }
            return "(no date)";
        }

        public static string GetFriendlyAge(DateTime dt)
        {
            DateTime dttz = dt.ToLocalTime();
            var s = dttz.ToString("yyyy-MM-dd HH:mm:ss") + " (";
            var dtNow = DateTime.Now;
            if (dttz.Date == dtNow.Date)
            {
                s += "today";
            }
            else if (dttz.Date == dtNow.AddDays(-1).Date)
            {
                s += "yesterday";
            }
            else
            {
                var td = (dtNow.Date - dttz).TotalDays;
                var d = Math.Ceiling(td);
                s += d + " days ago";
            }
            return s + ")";
        }

        public static string ToHumanReadableString(this TimeSpan t)
        {
            // from http://stackoverflow.com/a/36191436
            if (t.TotalSeconds <= 1)
            {
                return $@"{t:s\.ff} seconds";
            }
            if (t.TotalMinutes <= 1)
            {
                return $@"{t:%s} seconds";
            }
            if (t.TotalHours <= 1)
            {
                return $@"{t:%m} minutes";
            }
            if (t.TotalDays <= 1)
            {
                return $@"{t:%h} hours";
            }

            return $@"{t:%d} days";
        }
        
        /// <summary> 
        /// Removes all tags from a string of HTML, leaving just the text content.
        /// 
        /// Text inside tag bodies is preserved.
        /// 
        /// TODO: This is exactly the same as HtmlUtils.TextOnly !!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string StripHtml(this string s)
        {
            return Regex.Replace(s, @"<(.|\n)*?>", string.Empty);
        }
        
        /// <summary>
        /// Removes any HTML, then truncates text cleanly, breaking on a word-boundary (space), such that the
        /// returned text length is less than or equal to maxChars
        /// 
        /// "I do not like <b>green</b> eggs and ham", 27 => "I do not like green eggs..."
        /// 
        /// Useful for generating summary text
        /// </summary>
        /// <param name="fullField"></param>
        /// <param name="maxChars"></param>
        /// <returns></returns>
        public static string SummariseWithEllipsis(this string fullField, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(fullField)) return fullField;

                string stripped = fullField.StripHtml();
            if (stripped.Length <= maxChars)
                return stripped;

            stripped = stripped.Substring(0, maxChars);
            int lastSpace = stripped.LastIndexOf(" ", StringComparison.Ordinal);
            if (lastSpace == -1)
            {
                return stripped.Substring(0, maxChars);
            }
            return stripped.Substring(0, lastSpace) + "...";
        }

        /// <summary>
        /// True if EVERY supplied string has significant content.
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        public static bool AllHaveText(params string[] strings)
        {
            return strings.AllHaveText();
        }

        /// <summary>
        /// True if EVERY supplied string has significant content.
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        public static bool AllHaveText(this IEnumerable<string> strings)
        {
            foreach (string s in strings)
            {
                if (!HasText(s)) return false;
            }
            return true;
        }

        /// <summary>
        /// True if ANY supplied string has significant content.
        /// </summary>
        /// <param name="tests"></param>
        /// <returns></returns>
        public static bool AnyHaveText(params string[] tests)
        {
            foreach (string s in tests)
            {
                if (s.HasText())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Inversion of AnyHaveText, for readability
        /// </summary>
        /// <param name="tests"></param>
        /// <returns></returns>
        public static bool NoneHaveText(params string[] tests)
        {
            return !AnyHaveText(tests);
        }

        /// <summary>
        /// attempt to parse a boolean value from common representations (more flexible than bool.Parse)
        /// 
        /// "yes" => true
        /// "y" => true
        /// "1" => true
        /// "YES" => true
        /// "trUE" => true
        /// "0" => false
        /// "NO" => false
        /// 
        /// ... and so on.
        /// 
        /// "", false => false
        /// "", true => true
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="valueIfEmpty">if string s doesn't contain any text, return this value</param>
        /// <returns></returns>
        public static bool GetBoolValue(string s, bool valueIfEmpty)
        {
            if (!s.HasText())
            {
                return valueIfEmpty;
            }
            s = s.ToLowerInvariant().Trim();
            if (s.StartsWith("y") || s == "1" || s == "true")
            {
                return true;
            }
            return false;
        }
    }
}