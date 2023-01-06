using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils
{
    public static class StringUtils
    {
        private static readonly string[] FileSizeSuffixes;
        static StringUtils()
        {
            //Longs run out around EB
            FileSizeSuffixes = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        }
        
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        /// <summary>
        /// Does this string have significant content (is not null, empty, or just whitespace character(s))
        /// </summary>
        /// <remarks>
        /// This may seem trivial but it helps code readability.
        /// </remarks>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool HasText([NotNullWhen(true)] this string? str) => !string.IsNullOrWhiteSpace(str);

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
        public static string? RemoveStart(this string? str, string start)
        {
            if (str == null) return null;
            if (str == string.Empty) return string.Empty;

            if (str.StartsWith(start) && str.Length > start.Length)
            {
                return str.Substring(start.Length);
            }

            return str;
        }

        public static DateTime? GetNullableDateTime(string? s)
        {
            if (DateTime.TryParse(s, out var date))
            {
                return date;
            }

            return null;
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
        /// remove leading and trailing characters that are not alphanumeric
        /// TODO - improve this, not very efficient
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string TrimNonAlphaNumeric(this string s)
        {
            var sb = new StringBuilder();
            bool inAlphas = false;
            foreach (char c in s)
            {
                if (inAlphas || Char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    inAlphas = true;
                }
            }
            // we now have a string with alphas on the end
            var sr = sb.ToString().Reverse();
            var sbr = new StringBuilder();
            foreach (char c in sr)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sbr.Append(c);
                }
            }
            var array = sbr.ToString().ToCharArray();
            Array.Reverse(array);
            return new String(array);
        }

        /// <summary>
        /// Strips out any character that is not a letter or a digit or an underscore "_".
        /// This is useful when constructing a simple name for a content item you are creating 
        /// programmatically (e.g., in migration) as it ensures it will be permitted in a URL
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToAlphanumericOrUnderscore(this string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (Char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary> 
        /// Strips out any character that is not a letter or a digit or whitespace.
        /// e.g., removes all punctuation, apostrophes etc.
        /// Useful when preparing terms for submission to a search engine.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="exceptions"></param>
        /// <returns></returns>
        public static string ToAlphanumericOrWhitespace(this string s, char[]? exceptions = null)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
                if (exceptions.HasItems() && exceptions.Contains(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// strips out any character that is not part of a parseable number.
        /// 
        /// "23a" => "23"
        /// "23.4" => "23.4"
        /// "-100" => "-100"
        /// "3 Blind Mice" => "3"
        /// "£9.99" => "9.99"
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToNumber(this string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (Char.IsDigit(c) || c == '.' || c == '-')
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
        public static string[] SplitByDelimiterIntoArray(this string? source, char delimiter)
        {
            var strings = SplitByDelimiter(source, delimiter);
            if (strings == null)
            {
                return Array.Empty<string>();
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
        public static IEnumerable<string>? SplitByDelimiter(this string? source, char delimiter)
        {
            if (string.IsNullOrEmpty(source)) return null;
            
            // this trims whitespace by default - implement another one if required
            var strings = source.Split(new[] { delimiter });
            return strings.Where(s => s.HasText()).Select(s => s.Trim());
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
            if (str.IsNullOrEmpty()) return str;
            if (str.EndsWith(separator))
            {
                return str[..str.LastIndexOf(separator, StringComparison.Ordinal)];
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
        /// <param name="withSpace">include a space between number and unit</param>
        /// <returns></returns>
        public static string FormatFileSize(long sizeInBytes, bool withSpace = false)
        {
            var spacer = withSpace ? " " : "";
            if (sizeInBytes == 0)
                return "0" + spacer + FileSizeSuffixes[0];
            long bytes = Math.Abs(sizeInBytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(sizeInBytes) * num) + spacer +  FileSizeSuffixes[place];
        }

        /// <summary> 
        /// Create a nice display format for file size given a raw string value
        /// </summary>
        /// <param name="rawSize"></param>
        /// <param name="withSpace"></param>
        /// <returns></returns>
        public static string? FormatFileSize(string? rawSize, bool withSpace = false)
        {
            if (long.TryParse(rawSize, out var asLong))
            {
                return FormatFileSize(asLong, withSpace);
            }
            return rawSize;
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
            DateTime localTime = dt.ToLocalTime();
            var s = localTime.ToString("yyyy-MM-dd HH:mm:ss") + " (";
            var dtNow = DateTime.Now;
            if (localTime.Date == dtNow.Date)
            {
                s += "today";
            }
            else if (localTime.Date == dtNow.AddDays(-1).Date)
            {
                s += "yesterday";
            }
            else
            {
                var td = (dtNow.Date - localTime).TotalDays;
                var d = Math.Ceiling(td);
                s += d + " days ago";
            }
            return s + ")";
        }
        
        public static string? GetFileName(this string s)
        {
            var parts = s.Split(new [] {'/', '\\'});
            return parts.LastOrDefault();
        }
        
        public static string GetFileExtension(this string s)
        {
            string? fn = GetFileName(s);
            if (fn.IsNullOrWhiteSpace())
            {
                return String.Empty;
            }
            var idx = fn.LastIndexOf('.');
            if (idx != -1 && fn.Length > idx)
            {
                return fn.Substring(idx + 1);
            }
            return String.Empty;
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
        public static bool AllHaveText(params string?[] strings)
        {
            return strings.AllHaveText();
        }

        /// <summary>
        /// True if EVERY supplied string has significant content.
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        public static bool AllHaveText(this IEnumerable<string?> strings)
        {
            foreach (string? s in strings)
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
        public static bool AnyHaveText(params string?[] tests)
        {
            return tests.Any(s => s.HasText());
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
        
        
        
        public static string ReplaceFromDictionary(this string s, Dictionary<string, string> dict)
        {
            // https://stackoverflow.com/a/14033595
            return dict.Aggregate(s, (current, kvp) => current.Replace(kvp.Key, kvp.Value));
        }
        
        public static string ReplaceFromDictionary(this string s, Dictionary<string, string> dict, string template)
        {
            // https://stackoverflow.com/a/14033595
            // return dict.Aggregate(s, (current, kvp) => current.Replace(kvp.Key, string.Format(template, kvp.Key, kvp.Value)));

            var byLength = dict.OrderByDescending(kvp => kvp.Key.Length).ToArray();
            for (var index = 0; index < byLength.Length; index++)
            {
                var pair = byLength[index];
                s = s.Replace(pair.Key, $"%%${index}$%%");
            }

            for (var index = 0; index < byLength.Length; index++)
            {
                var pair = byLength[index];
                s = s.Replace($"%%${index}$%%", string.Format(template, pair.Key, pair.Value));
            }
            return s;
        }

        /// <summary>
        /// Finds an operation and its required parameter from a string array (command line args).
        /// Not sophisticated, no parameterless args
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="permittedOperations"></param>
        /// <returns></returns>
        public static (string? operation, string? parameter) GetOperationAndParameter(string[] arguments, string[] permittedOperations)
        {
            string? operation = null, parameter = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                var op = arguments[i];
                if (permittedOperations.Contains(op))
                {
                    operation = op;
                    i++;
                    if (i < arguments.Length)
                    {
                        parameter = arguments[i];
                    }
                }
            }
            return (operation, parameter);
        }

        public static int? ToNullableInt(this string? s)
        {
            if (int.TryParse(s, out var i)) return i;
            return null;
        }
        
        public static double? ToNullableDouble(this string? s)
        {
            if (double.TryParse(s, out var i)) return i;
            return null;
        }
        
        
        private static readonly Regex Roman = new Regex(
            "^M{0,4}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$",
            RegexOptions.IgnoreCase);

        public static bool IsRomanNumeral(string s)
        {
            return Roman.IsMatch(s);
        }
    }
}