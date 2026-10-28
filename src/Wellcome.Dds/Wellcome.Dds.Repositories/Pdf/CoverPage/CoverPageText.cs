using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// Turns the HTML fragments that appear in manifest metadata ("Attribution and usage" is one) into
/// plain paragraphs, and keeps text within what the embedded fonts can show.
/// </summary>
public static class CoverPageText
{
    private static readonly Regex LineBreaks = new("<br\\s*/?>", RegexOptions.IgnoreCase);
    private static readonly Regex Tags = new("<[^>]+>");
    private static readonly Regex Spaces = new("[ \\t\\r\\n]+");

    /// <summary>
    /// Split an HTML fragment into paragraphs at line breaks, strip the remaining tags and decode entities.
    /// A run of line breaks is one paragraph break, as it reads on the page.
    /// </summary>
    public static List<string> ToParagraphs(string html)
    {
        return LineBreaks.Split(html)
            .Select(part => Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(part, "")), " ").Trim())
            .Where(part => part.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Inter covers Latin, Greek and Cyrillic; the PDF font is a simple 1-byte font so it cannot show
    /// characters outside the Basic Multilingual Plane's lower ranges at all. Anything else becomes '?',
    /// which is what the previous service produced for such characters too.
    /// </summary>
    public static string Printable(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(IsPrintable(c) ? c : '?');
        }
        return sb.ToString();
    }

    private static bool IsPrintable(char c)
    {
        if (c < 0x20)
        {
            return false;
        }
        // Latin, Latin-1, Latin Extended A/B, IPA, spacing modifiers, Greek, Cyrillic, Latin Extended Additional
        if (c <= 0x052F || (c >= 0x1E00 && c <= 0x1EFF))
        {
            return true;
        }
        // General punctuation (dashes, quotes, ellipsis), currency, letterlike symbols, arrows
        return c >= 0x2010 && c <= 0x2199;
    }
}
