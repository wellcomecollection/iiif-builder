using System;
using System.Collections.Generic;
using NetVips;

namespace Wellcome.Dds.Server.Images;

/// <summary>
/// Draws the PNG painted onto a born-digital placeholder canvas: a flat background and four lines of
/// text naming the file's MIME type and PRONOM key. Rendered with libvips (NetVips); the font is
/// loaded from a file so nothing needs to be installed on the host.
/// </summary>
public static class PlaceholderCanvasRenderer
{
    public const int Width = 1000;
    public const int Height = 800;

    private const string FontFamily = "Wellcome Bold";
    private const int Margin = 20;
    private static readonly double[] Background = { 237, 236, 228 };

    /// <summary>
    /// A line's text, font size in pixels, and the y it is drawn at when nothing above it has wrapped.
    /// </summary>
    private record Line(string Text, int Size, int Y);

    public static byte[] RenderPng(string fontFilePath, string mimeType, string pronomKey)
    {
        var lines = new List<Line>
        {
            new($"File type: {mimeType}", 32, 20),
            new($"PRONOM key: {pronomKey}", 32, 80),
            new("This is a placeholder canvas.", 16, 140),
            new("The original file is available in the rendering property of this canvas.", 16, 165)
        };

        // Cast drops the colour interpretation to "multiband", which composite2 cannot convert; say it is sRGB.
        using var background = (Image.Black(Width, Height, bands: 3) + Background)
            .Cast(Enums.BandFormat.Uchar)
            .Copy(interpretation: Enums.Interpretation.Srgb);
        var canvas = background;
        try
        {
            // A long MIME type (e.g. a docx one) wraps within the margins, so the lines below it move
            // down by the extra height; when nothing wraps the lines sit at their fixed positions.
            var shift = 0;
            foreach (var line in lines)
            {
                // Image.Text takes Pango markup, so the dynamic parts must be escaped.
                using var rendered = Image.Text(Escape(line.Text),
                    font: $"{FontFamily} {line.Size}",
                    fontfile: fontFilePath,
                    width: Width - 2 * Margin,
                    rgba: true,
                    dpi: 72);
                var next = canvas.Composite2(rendered, Enums.BlendMode.Over, x: Margin, y: line.Y + shift);
                if (!ReferenceEquals(canvas, background))
                {
                    canvas.Dispose();
                }
                canvas = next;
                shift += ExtraHeight(rendered.Height, line.Size);
            }
            // Compositing RGBA text leaves an alpha band; the image is fully opaque, so drop it.
            using var flattened = canvas.Flatten();
            return flattened.PngsaveBuffer();
        }
        finally
        {
            if (!ReferenceEquals(canvas, background))
            {
                canvas.Dispose();
            }
        }
    }

    /// <summary>
    /// How much taller than a single line the rendered text is. A single line is a little taller than
    /// its font size (ascenders and descenders), so allow a quarter again before calling it wrapped.
    /// </summary>
    private static int ExtraHeight(int renderedHeight, int fontSize)
    {
        var singleLineAllowance = fontSize * 5 / 4;
        return Math.Max(0, renderedHeight - singleLineAllowance);
    }

    private static string Escape(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
