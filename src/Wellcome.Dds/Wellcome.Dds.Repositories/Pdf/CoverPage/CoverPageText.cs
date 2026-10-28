using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// A run of text in a paragraph, optionally a link. Links in the attribution text ("Creative Commons,
/// Public Domain Mark") were clickable on the old service's cover page and still are.
/// </summary>
public class CoverSegment
{
    public CoverSegment(string text, string? url = null)
    {
        Text = text;
        Url = url;
    }

    public string Text { get; set; }
    public string? Url { get; }
}

/// <summary>
/// One paragraph of plain text, as a sequence of segments some of which are links.
/// </summary>
public class CoverParagraph
{
    public CoverParagraph(params CoverSegment[] segments)
    {
        Segments.AddRange(segments);
    }

    public List<CoverSegment> Segments { get; } = new();

    public string Text => string.Concat(Segments.Select(s => s.Text));

    /// <summary>
    /// A paragraph that is one link, or plain text when <paramref name="url"/> is null.
    /// </summary>
    public static CoverParagraph Plain(string text, string? url = null) => new(new CoverSegment(text, url));
}

/// <summary>
/// Turns the HTML fragments that appear in manifest metadata ("Attribution and usage" is one) into
/// plain paragraphs, keeping the links, and keeps text to what a font can show.
/// </summary>
public static class CoverPageText
{
    public const char Substitute = '?';

    private static readonly Regex LineBreaks = new("<br\\s*/?>", RegexOptions.IgnoreCase);
    private static readonly Regex Tag = new("<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>");
    private static readonly Regex Href = new("href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);

    /// <summary>
    /// Split an HTML fragment into paragraphs at line breaks; strip the tags, keeping the text of each
    /// <c>&lt;a href&gt;</c> as a link segment; decode entities; collapse whitespace. A run of line breaks
    /// is one paragraph break, as it reads on the page. Plain text is one paragraph of one segment.
    /// </summary>
    public static List<CoverParagraph> ToParagraphs(string html)
    {
        return LineBreaks.Split(html)
            .Select(ToParagraph)
            .Where(p => p.Segments.Count > 0)
            .ToList();
    }

    private static CoverParagraph ToParagraph(string fragment)
    {
        var paragraph = new CoverParagraph();
        var text = new StringBuilder();
        string? currentUrl = null;
        var lastWasSpace = true; // so leading whitespace is dropped

        void Flush()
        {
            if (text.Length > 0)
            {
                paragraph.Segments.Add(new CoverSegment(text.ToString(), currentUrl));
                text.Clear();
            }
        }

        void AppendText(string raw)
        {
            foreach (var c in WebUtility.HtmlDecode(raw))
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        text.Append(' ');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    text.Append(c);
                    lastWasSpace = false;
                }
            }
        }

        var position = 0;
        foreach (Match tag in Tag.Matches(fragment))
        {
            AppendText(fragment.Substring(position, tag.Index - position));
            position = tag.Index + tag.Length;
            if (!tag.Groups[2].Value.Equals("a", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (tag.Groups[1].Value == "/")
            {
                Flush();
                currentUrl = null;
            }
            else
            {
                var href = Href.Match(tag.Groups[3].Value);
                var url = href.Success ? WebUtility.HtmlDecode(href.Groups[1].Value + href.Groups[2].Value + href.Groups[3].Value) : "";
                Flush();
                currentUrl = url.Length > 0 ? url : null;
            }
        }
        AppendText(fragment.Substring(position));
        Flush();

        // trailing space, and any segment that was only whitespace
        var last = paragraph.Segments.LastOrDefault();
        if (last != null)
        {
            last.Text = last.Text.TrimEnd();
        }
        paragraph.Segments.RemoveAll(s => s.Text.Length == 0);
        return paragraph;
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
