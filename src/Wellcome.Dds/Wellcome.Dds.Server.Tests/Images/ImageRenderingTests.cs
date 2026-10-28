using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NetVips;
using Wellcome.Dds.Server.Images;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Images;

/// <summary>
/// Exercises the libvips-backed rendering that replaced ImageSharp, with the font file the server ships.
/// </summary>
public class ImageRenderingTests
{
    static ImageRenderingTests()
    {
        // as Startup sets it: the text and PNG/JPEG paths used here must work with untrusted loaders blocked
        NetVips.NetVips.BlockUntrusted = true;
    }

    private static readonly string FontPath = Path.Combine(AppContext.BaseDirectory, "Resources", "born-digital", "wellcome-bold.ttf");

    // A real born-digital case: Word documents have one of the longest MIME types we will see.
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public void Placeholder_Canvas_Is_A_1000_By_800_Png_With_Text_On_It()
    {
        File.Exists(FontPath).Should().BeTrue($"the test copies the server's font to {FontPath}");

        var png = PlaceholderCanvasRenderer.RenderPng(FontPath, "application/pdf", "fmt/276");
        // Left beside the test assembly for eyeballing
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "placeholder-sample.png"), png);

        png.Should().StartWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "it is a PNG");
        using var image = Image.PngloadBuffer(png);
        image.Width.Should().Be(PlaceholderCanvasRenderer.Width);
        image.Height.Should().Be(PlaceholderCanvasRenderer.Height);
        image.Bands.Should().Be(3);
        // The background is (237, 236, 228); black text means the darkest pixel is far below that.
        image.Min().Should().BeLessThan(64, "text was drawn");
        // A pixel in the bottom-right, away from the text, is still background.
        image.Getpoint(PlaceholderCanvasRenderer.Width - 10, PlaceholderCanvasRenderer.Height - 10)
            .Should().Equal(237, 236, 228);

        // Four lines of text, each a single line high, at their fixed positions.
        var bands = InkBands(image);
        bands.Should().HaveCount(4);
        bands.Select(b => b.top).Should().Equal(20, 80, 140, 165); // the y each line is drawn at
        bands.Should().OnlyContain(b => b.height <= 40, "no line wrapped");
    }

    [Fact]
    public void Placeholder_Canvas_Moves_Lower_Lines_Down_When_The_Mime_Type_Wraps()
    {
        var png = PlaceholderCanvasRenderer.RenderPng(FontPath, DocxMimeType, "fmt/412");
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "placeholder-sample-docx.png"), png);

        using var image = Image.PngloadBuffer(png);
        var bands = InkBands(image);
        // The MIME type wraps onto two rows; those, the PRONOM line and the two sentences are five
        // separate bands of ink with background between them, so nothing overprints.
        bands.Should().HaveCount(5);
        bands.Should().OnlyContain(b => b.height <= 40, "each band is a single line of text");
        // Everything below the wrapped line has moved down, and the bottom sentence is still on the canvas.
        bands[2].top.Should().BeGreaterThan(80);
        bands[4].top.Should().BeLessThan(PlaceholderCanvasRenderer.Height - 20);
    }

    [Fact]
    public void Placeholder_Canvas_Escapes_Markup_In_The_Inputs()
    {
        // Image.Text takes Pango markup; an unescaped '<' would throw or vanish.
        var act = () => PlaceholderCanvasRenderer.RenderPng(FontPath, "text/<b>html</b>", "fmt/<1>");
        act.Should().NotThrow();
    }

    [Fact]
    public void Jpeg_Resize_Preserves_Aspect_Ratio_From_A_Buffer_And_From_A_File()
    {
        using var source = (Image.Black(2000, 1500, bands: 3) + new double[] { 200, 100, 50 }).Cast(Enums.BandFormat.Uchar);
        var sourceJpeg = source.JpegsaveBuffer();

        var fromBuffer = JpegResizer.ResizeToWidth(sourceJpeg, 600);
        using var resized = Image.JpegloadBuffer(fromBuffer);
        resized.Width.Should().Be(600);
        resized.Height.Should().Be(450);
        fromBuffer.Should().StartWith(new byte[] { 0xFF, 0xD8 }, "it is a JPEG");

        var path = Path.Combine(Path.GetTempPath(), $"jpegresizer-{Guid.NewGuid():N}.jpg");
        try
        {
            File.WriteAllBytes(path, sourceJpeg);
            var fromFile = JpegResizer.ResizeToWidth(path, 1024);
            using var resizedFromFile = Image.JpegloadBuffer(fromFile);
            resizedFromFile.Width.Should().Be(1024);
            resizedFromFile.Height.Should().Be(768);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(794, 1123, 200, 283)]   // an A4 page as Ghostscript renders it at 96 dpi, at the /thumb PDF default width
    [InlineData(794, 1123, 1024, 1448)] // the same page at the stored thumbnail width
    [InlineData(1000, 2000, 600, 1200)]
    public void Jpeg_Resize_Scales_Portrait_Images_To_The_Requested_Width(int sourceWidth, int sourceHeight, int width, int expectedHeight)
    {
        // libvips' Thumbnail fits inside width x height and defaults height to width, which would make
        // these come out narrower than asked; the resizer must leave height unbounded.
        using var source = (Image.Black(sourceWidth, sourceHeight, bands: 3) + 128).Cast(Enums.BandFormat.Uchar);
        var sourceJpeg = source.JpegsaveBuffer();

        using var fromBuffer = Image.JpegloadBuffer(JpegResizer.ResizeToWidth(sourceJpeg, width));
        fromBuffer.Width.Should().Be(width);
        fromBuffer.Height.Should().Be(expectedHeight);

        var path = Path.Combine(Path.GetTempPath(), $"jpegresizer-{Guid.NewGuid():N}.jpg");
        try
        {
            File.WriteAllBytes(path, sourceJpeg);
            using var fromFile = Image.JpegloadBuffer(JpegResizer.ResizeToWidth(path, width));
            fromFile.Width.Should().Be(width);
            fromFile.Height.Should().Be(expectedHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Jpeg_Resize_Upscales_Like_The_Previous_Implementation()
    {
        using var source = (Image.Black(100, 50, bands: 3) + 128).Cast(Enums.BandFormat.Uchar);
        var small = JpegResizer.ResizeToWidth(source.JpegsaveBuffer(), 400);
        using var resized = Image.JpegloadBuffer(small);
        resized.Width.Should().Be(400);
        resized.Height.Should().Be(200);
    }

    /// <summary>
    /// Contiguous runs of rows containing dark pixels, as (top, height). Two lines of text that overprint
    /// merge into one tall band; lines with background between them are separate bands.
    /// </summary>
    private static List<(int top, int height)> InkBands(Image image)
    {
        // One band, 255 where any channel is dark, then sum along each row: non-zero rows contain ink.
        using var grey = image.Bandmean();
        using var ink = grey < 128;
        var projected = ink.Project(); // object[] { column sums, row sums }, both uint images
        using var columns = (Image) projected[0];
        using var rowsImage = (Image) projected[1];
        var rowSums = rowsImage.WriteToMemory<uint>();

        var bands = new List<(int top, int height)>();
        var inBand = false;
        var top = 0;
        for (var y = 0; y < rowSums.Length; y++)
        {
            var hasInk = rowSums[y] > 0;
            if (hasInk && !inBand)
            {
                inBand = true;
                top = y;
            }
            else if (!hasInk && inBand)
            {
                inBand = false;
                bands.Add((top, y - top));
            }
        }
        if (inBand)
        {
            bands.Add((top, rowSums.Length - top));
        }
        return bands;
    }
}
