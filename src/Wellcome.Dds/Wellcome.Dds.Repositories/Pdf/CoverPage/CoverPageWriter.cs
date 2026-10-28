using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// Writes the cover page PDF with PdfPig: an A4 page (more if the text overflows) laid out as the
/// retired PdfCoverPage service's reportlab template was: title, headed sections, the attribution
/// text, and a footer with the provider's logo and address.
/// </summary>
public class CoverPageWriter
{
    // reportlab's SimpleDocTemplate: 72pt side margins, 30pt top and bottom, 6pt frame padding
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Left = 78;
    private const double Right = PageWidth - 78;
    private const double Top = PageHeight - 36;
    private const double Bottom = 36;
    private const double ContentWidth = Right - Left;

    private const double HeadingSize = 12, HeadingLeading = 15;
    private const double BodySize = 12, BodyLeading = 14;
    private const double FooterSize = 11, FooterLeading = 13;

    private const double LogoWidth = 200, LogoHeight = 66, LogoBottomPadding = 10;
    private const double FooterGap = 20;

    /// <summary>
    /// PdfPig writes a simple (one byte per character) TrueType font, so each font can show at most
    /// this many distinct characters in one document; beyond it, characters become '?'.
    /// </summary>
    public const int MaxDistinctCharactersPerFont = 255;

    private readonly CoverPageFont regularFont;
    private readonly CoverPageFont boldFont;

    public CoverPageWriter() : this(CoverPageFonts.Regular, CoverPageFonts.Bold)
    {
    }

    public CoverPageWriter(CoverPageFont regularFont, CoverPageFont boldFont)
    {
        this.regularFont = regularFont;
        this.boldFont = boldFont;
    }

    /// <summary>
    /// Whether PdfPig can place these bytes as an image: a PNG or JPEG it can decode. Checked when a
    /// logo is fetched, so a bad logo is dropped there rather than failing every cover page.
    /// </summary>
    public static bool IsUsableLogo(byte[]? logo)
    {
        if (logo == null || logo.Length < 8)
        {
            return false;
        }
        try
        {
            var scratch = new PdfDocumentBuilder();
            PlaceLogo(scratch.AddPage(PageWidth, PageHeight), logo, new PdfRectangle(0, 0, LogoWidth, LogoHeight));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <param name="logo">The provider's logo as PNG or JPEG bytes, or null to leave the space empty.</param>
    public byte[] Write(CoverPageContent content, byte[]? logo)
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = $"{content.Identifier}.pdf";
        var regular = new Face(regularFont, builder.AddTrueTypeFont(regularFont.Bytes));
        var bold = new Face(boldFont, builder.AddTrueTypeFont(boldFont.Bytes));
        var layout = new Layout(builder);

        layout.Paragraph(CoverParagraph.Plain(content.Title), bold, HeadingSize, HeadingLeading);
        layout.Space(30);

        foreach (var section in content.Sections)
        {
            layout.Paragraph(CoverParagraph.Plain(section.Heading), bold, HeadingSize, HeadingLeading);
            layout.Space(6);
            foreach (var line in section.Lines)
            {
                foreach (var paragraph in CoverPageText.ToParagraphs(line))
                {
                    layout.Paragraph(section.LinkUrl == null ? paragraph : CoverParagraph.Plain(paragraph.Text, section.LinkUrl),
                        regular, BodySize, BodyLeading);
                }
            }
            layout.Space(10);
        }

        layout.Space(20);
        if (content.Attribution.Count > 0)
        {
            layout.Paragraph(CoverParagraph.Plain("License and attribution"), bold, HeadingSize, HeadingLeading);
            layout.Space(6);
            foreach (var line in content.Attribution)
            {
                var paragraphs = CoverPageText.ToParagraphs(line);
                for (var i = 0; i < paragraphs.Count; i++)
                {
                    layout.Paragraph(paragraphs[i], regular, BodySize, BodyLeading);
                    if (i < paragraphs.Count - 1)
                    {
                        layout.Space(BodyLeading); // a <br/><br/> in the source reads as a blank line
                    }
                }
            }
        }

        layout.Footer(logo, content.ProviderAddress, regular);
        return builder.Build();
    }

    private static void PlaceLogo(PdfPageBuilder page, byte[] logo, PdfRectangle placement)
    {
        if (logo.Length > 2 && logo[0] == 0xFF && logo[1] == 0xD8)
        {
            page.AddJpeg(logo, placement);
        }
        else
        {
            page.AddPng(logo, placement);
        }
    }

    /// <summary>
    /// A font as used in one document: which characters it has, and which it has used so far, so that
    /// text can be made safe for PdfPig before it is measured or drawn.
    /// </summary>
    private class Face
    {
        private readonly CoverPageFont font;
        private readonly HashSet<char> used = new() { CoverPageText.Substitute, ' ' };

        public Face(CoverPageFont font, PdfDocumentBuilder.AddedFont added)
        {
            this.font = font;
            Added = added;
        }

        public PdfDocumentBuilder.AddedFont Added { get; }

        /// <summary>
        /// Characters the font cannot show, or that would take it past PdfPig's per-font limit, become '?'.
        /// Character for character, so offsets into the text are unchanged.
        /// </summary>
        public string Fit(string text)
        {
            var chars = CoverPageText.Printable(text, font).ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (used.Contains(chars[i]))
                {
                    continue;
                }
                if (used.Count < MaxDistinctCharactersPerFont)
                {
                    used.Add(chars[i]);
                }
                else
                {
                    chars[i] = CoverPageText.Substitute;
                }
            }
            return new string(chars);
        }
    }

    /// <summary>
    /// A link over a range of characters of a paragraph's text.
    /// </summary>
    private readonly record struct LinkSpan(int Start, int End, string Url);

    /// <summary>
    /// A top-down cursor over as many pages as the text needs, with word wrapping measured in the
    /// actual font. The footer goes at the bottom of the last page.
    /// </summary>
    private class Layout
    {
        private readonly PdfDocumentBuilder builder;
        private PdfPageBuilder page;
        private double y;

        public Layout(PdfDocumentBuilder builder)
        {
            this.builder = builder;
            page = builder.AddPage(PageWidth, PageHeight);
            y = Top;
        }

        private static double FooterHeight => Math.Max(LogoHeight + LogoBottomPadding, 6 * FooterLeading) + FooterGap;

        public void Space(double points)
        {
            y -= points;
        }

        public void Paragraph(CoverParagraph paragraph, Face face, double size, double leading)
        {
            // Fit each segment on its own: the substitution is character for character, so the segment
            // lengths give the link spans in the fitted text.
            var text = "";
            var links = new List<LinkSpan>();
            foreach (var segment in paragraph.Segments)
            {
                var fitted = face.Fit(segment.Text);
                if (segment.Url != null && fitted.Length > 0)
                {
                    links.Add(new LinkSpan(text.Length, text.Length + fitted.Length, segment.Url));
                }
                text += fitted;
            }

            foreach (var (lineStart, lineText) in Wrap(text, face.Added, size))
            {
                if (y - leading < Bottom + FooterHeight)
                {
                    page = builder.AddPage(PageWidth, PageHeight);
                    y = Top;
                }
                var baseline = y - size;
                var letters = page.AddText(lineText, size, new PdfPoint(Left, baseline), face.Added);
                var lineEnd = lineStart + lineText.Length;
                foreach (var link in links)
                {
                    // the part of this link that falls on this line, as indexes into the line
                    var from = Math.Max(link.Start, lineStart) - lineStart;
                    var to = Math.Min(link.End, lineEnd) - lineStart;
                    if (from >= to || from >= letters.Count)
                    {
                        continue;
                    }
                    to = Math.Min(to, letters.Count);
                    var x1 = letters[from].StartBaseLine.X;
                    var x2 = letters[to - 1].EndBaseLine.X;
                    page.AddLink(link.Url, new PdfRectangle(x1, baseline - 3, x2, baseline + size));
                }
                y -= leading;
            }
        }

        public void Footer(byte[]? logo, List<string> addressLines, Face face)
        {
            var logoBottom = Bottom + LogoBottomPadding;
            var logoTop = logoBottom + LogoHeight;
            if (logo != null && logo.Length > 0)
            {
                PlaceLogo(page, logo, new PdfRectangle(Left, logoBottom, Left + LogoWidth, logoTop));
            }

            // the address sits to the right of the logo, top-aligned with it
            var x = Left + LogoWidth + 24;
            var baseline = logoTop - FooterSize;
            foreach (var line in addressLines)
            {
                page.AddText(face.Fit(line), FooterSize, new PdfPoint(x, baseline), face.Added);
                baseline -= FooterLeading;
            }
        }

        /// <summary>
        /// Word-wrap the text to the content width, yielding each line with its start offset in the text
        /// so links can be placed by character position. Words are runs between spaces; a word wider
        /// than the page is broken by character.
        /// </summary>
        private IEnumerable<(int Start, string Text)> Wrap(string text, PdfDocumentBuilder.AddedFont font, double size)
        {
            var words = new List<(int Start, int End)>();
            var i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }
                var start = i;
                while (i < text.Length && text[i] != ' ')
                {
                    i++;
                }
                if (i > start)
                {
                    words.Add((start, i));
                }
            }
            if (words.Count == 0)
            {
                yield break;
            }

            var lineStart = words[0].Start;
            var lineEnd = words[0].End;
            foreach (var word in words.Skip(1))
            {
                if (Width(text[lineStart..word.End], font, size) <= ContentWidth)
                {
                    lineEnd = word.End;
                    continue;
                }
                foreach (var piece in Broken(text, lineStart, lineEnd, font, size))
                {
                    yield return piece;
                }
                lineStart = word.Start;
                lineEnd = word.End;
            }
            foreach (var piece in Broken(text, lineStart, lineEnd, font, size))
            {
                yield return piece;
            }
        }

        /// <summary>
        /// The line as one piece, or, if it is a single word wider than the page, broken by character.
        /// </summary>
        private IEnumerable<(int Start, string Text)> Broken(string text, int start, int end, PdfDocumentBuilder.AddedFont font, double size)
        {
            while (end - start > 1 && Width(text[start..end], font, size) > ContentWidth)
            {
                var fit = end - 1;
                while (fit > start + 1 && Width(text[start..fit], font, size) > ContentWidth)
                {
                    fit--;
                }
                yield return (start, text[start..fit]);
                start = fit;
            }
            yield return (start, text[start..end]);
        }

        private double Width(string text, PdfDocumentBuilder.AddedFont font, double size)
        {
            var letters = page.MeasureText(text, size, new PdfPoint(0, 0), font);
            return letters.Count == 0 ? 0 : letters.Max(l => l.GlyphRectangle.Right);
        }
    }
}
