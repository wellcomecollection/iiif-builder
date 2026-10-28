using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig.Content;
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

    private readonly byte[] regularFont;
    private readonly byte[] boldFont;

    public CoverPageWriter() : this(CoverPageFonts.Regular, CoverPageFonts.Bold)
    {
    }

    public CoverPageWriter(byte[] regularFont, byte[] boldFont)
    {
        this.regularFont = regularFont;
        this.boldFont = boldFont;
    }

    /// <param name="logo">The provider's logo as PNG or JPEG bytes, or null to leave the space empty.</param>
    public byte[] Write(CoverPageContent content, byte[]? logo)
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = $"{content.Identifier}.pdf";
        var regular = builder.AddTrueTypeFont(regularFont);
        var bold = builder.AddTrueTypeFont(boldFont);
        var layout = new Layout(builder, regular, bold);

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

        layout.Footer(logo, content.ProviderAddress);
        return builder.Build();
    }

    /// <summary>
    /// A top-down cursor over as many pages as the text needs, with word wrapping measured in the
    /// actual font. The footer goes at the bottom of the last page.
    /// </summary>
    private class Layout
    {
        private readonly PdfDocumentBuilder builder;
        private readonly PdfDocumentBuilder.AddedFont regular;
        private PdfPageBuilder page;
        private double y;

        public Layout(PdfDocumentBuilder builder, PdfDocumentBuilder.AddedFont regular, PdfDocumentBuilder.AddedFont bold)
        {
            this.builder = builder;
            this.regular = regular;
            page = builder.AddPage(PdfPig_A4Width, PdfPig_A4Height);
            y = Top;
        }

        private const double PdfPig_A4Width = PageWidth;
        private const double PdfPig_A4Height = PageHeight;

        private static double FooterHeight => Math.Max(LogoHeight + LogoBottomPadding, 6 * FooterLeading) + FooterGap;

        public void Space(double points)
        {
            y -= points;
        }

        public void Paragraph(string text, PdfDocumentBuilder.AddedFont font, double size, double leading, string? linkUrl = null)
        {
            foreach (var line in Wrap(CoverPageText.Printable(text), font, size))
            {
                if (y - leading < Bottom + FooterHeight)
                {
                    page = builder.AddPage(PdfPig_A4Width, PdfPig_A4Height);
                    y = Top;
                }
                var baseline = y - size;
                var letters = page.AddText(line, size, new PdfPoint(Left, baseline), font);
                if (linkUrl != null && letters.Count > 0)
                {
                    var right = letters.Max(l => l.GlyphRectangle.Right);
                    page.AddLink(linkUrl, new PdfRectangle(Left, baseline - 3, right, baseline + size));
                }
                y -= leading;
            }
        }

        public void Footer(byte[]? logo, List<string> addressLines)
        {
            var logoBottom = Bottom + LogoBottomPadding;
            var logoTop = logoBottom + LogoHeight;
            if (logo != null && logo.Length > 0)
            {
                var placement = new PdfRectangle(Left, logoBottom, Left + LogoWidth, logoTop);
                if (IsJpeg(logo))
                {
                    page.AddJpeg(logo, placement);
                }
                else
                {
                    page.AddPng(logo, placement);
                }
            }

            // the address sits to the right of the logo, top-aligned with it
            var x = Left + LogoWidth + 24;
            var baseline = logoTop - FooterSize;
            foreach (var line in addressLines)
            {
                page.AddText(CoverPageText.Printable(line), FooterSize, new PdfPoint(x, baseline), regular);
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

        private static bool IsJpeg(byte[] bytes) => bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
    }
}
