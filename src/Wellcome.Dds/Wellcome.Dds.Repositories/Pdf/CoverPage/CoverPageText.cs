using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// Turns the HTML fragments that appear in manifest metadata ("Attribution and usage" is one) into
/// plain paragraphs, and keeps text to what a font can show.
/// </summary>
public static class CoverPageText
{
    public const char Substitute = '?';

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
    /// The text with every character the font has no glyph for replaced by <see cref="Substitute"/>
    /// (one per character, so an emoji or a CJK ideograph is one '?'), and whitespace reduced to spaces.
    /// </summary>
    public static string Printable(string text, CoverPageFont font)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                sb.Append(' ');
            }
            else if (rune.IsBmp && !Rune.IsControl(rune) && font.Has((char) rune.Value))
            {
                sb.Append((char) rune.Value);
            }
            else
            {
                sb.Append(Substitute);
            }
        }
        return sb.ToString();
    }
}
