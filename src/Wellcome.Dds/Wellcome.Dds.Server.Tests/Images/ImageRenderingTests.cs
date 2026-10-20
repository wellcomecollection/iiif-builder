using System;
using System.IO;
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
    private static readonly string FontPath = Path.Combine(AppContext.BaseDirectory, "born-digital", "wellcome-bold.ttf");

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

    [Fact]
    public void Jpeg_Resize_Upscales_Like_The_Previous_Implementation()
    {
        using var source = (Image.Black(100, 50, bands: 3) + 128).Cast(Enums.BandFormat.Uchar);
        var small = JpegResizer.ResizeToWidth(source.JpegsaveBuffer(), 400);
        using var resized = Image.JpegloadBuffer(small);
        resized.Width.Should().Be(400);
        resized.Height.Should().Be(200);
    }
}
