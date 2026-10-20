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

    public static byte[] RenderPng(string fontFilePath, string mimeType, string pronomKey)
    {
        var lines = new List<(string text, int size, int y)>
        {
            ($"File type: {mimeType}", 32, 20),
            ($"PRONOM key: {pronomKey}", 32, 80),
            ("This is a placeholder canvas.", 16, 140),
            ("The original file is available in the rendering property of this canvas.", 16, 165)
        };

        // Cast drops the colour interpretation to "multiband", which composite2 cannot convert; say it is sRGB.
        using var background = (Image.Black(Width, Height, bands: 3) + Background)
            .Cast(Enums.BandFormat.Uchar)
            .Copy(interpretation: Enums.Interpretation.Srgb);
        var canvas = background;
        try
        {
            foreach (var (text, size, y) in lines)
            {
                // Image.Text takes Pango markup, so the dynamic parts must be escaped.
                using var rendered = Image.Text(Escape(text),
                    font: $"{FontFamily} {size}",
                    fontfile: fontFilePath,
                    width: Width - 2 * Margin,
                    rgba: true,
                    dpi: 72);
                var next = canvas.Composite2(rendered, Enums.BlendMode.Over, x: Margin, y: y);
                if (!ReferenceEquals(canvas, background))
                {
                    canvas.Dispose();
                }
                canvas = next;
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

    private static string Escape(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
