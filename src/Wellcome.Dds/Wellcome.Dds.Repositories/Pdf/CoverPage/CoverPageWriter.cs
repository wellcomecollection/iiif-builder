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

        layout.Paragraph(content.Title, bold, HeadingSize, HeadingLeading);
        layout.Space(30);

        foreach (var section in content.Sections)
        {
            layout.Paragraph(section.Heading, bold, HeadingSize, HeadingLeading);
            layout.Space(6);
            foreach (var line in section.Lines)
            {
                foreach (var paragraph in CoverPageText.ToParagraphs(line))
                {
                    layout.Paragraph(paragraph, regular, BodySize, BodyLeading, section.LinkUrl);
                }
            }
            layout.Space(10);
        }

        layout.Space(20);
        if (content.Attribution.Count > 0)
        {
            layout.Paragraph("License and attribution", bold, HeadingSize, HeadingLeading);
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
        /// </summary>
        public string Fit(string text)
        {
            var printable = CoverPageText.Printable(text, font);
            var chars = printable.ToCharArray();
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

        public void Paragraph(string text, Face face, double size, double leading, string? linkUrl = null)
        {
            foreach (var line in Wrap(face.Fit(text), face.Added, size))
            {
                if (y - leading < Bottom + FooterHeight)
                {
                    page = builder.AddPage(PageWidth, PageHeight);
                    y = Top;
                }
                var baseline = y - size;
                var letters = page.AddText(line, size, new PdfPoint(Left, baseline), face.Added);
                if (linkUrl != null && letters.Count > 0)
                {
                    var right = letters.Max(l => l.GlyphRectangle.Right);
                    page.AddLink(linkUrl, new PdfRectangle(Left, baseline - 3, right, baseline + size));
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

        private IEnumerable<string> Wrap(string text, PdfDocumentBuilder.AddedFont font, double size)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }
            var line = "";
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (Width(candidate, font, size) <= ContentWidth)
                {
                    line = candidate;
                    continue;
                }
                if (line.Length > 0)
                {
                    yield return line;
                }
                // a single word wider than the page: break it by character
                line = word;
                while (Width(line, font, size) > ContentWidth && line.Length > 1)
                {
                    var fit = line.Length - 1;
                    while (fit > 1 && Width(line[..fit], font, size) > ContentWidth)
                    {
                        fit--;
                    }
                    yield return line[..fit];
                    line = line[fit..];
                }
            }
            if (line.Length > 0)
            {
                yield return line;
            }
        }

        private double Width(string text, PdfDocumentBuilder.AddedFont font, double size)
        {
            var letters = page.MeasureText(text, size, new PdfPoint(0, 0), font);
            return letters.Count == 0 ? 0 : letters.Max(l => l.GlyphRectangle.Right);
        }
    }
}
