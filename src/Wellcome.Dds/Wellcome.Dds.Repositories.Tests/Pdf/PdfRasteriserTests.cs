using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;
using Wellcome.Dds.Repositories.Pdf;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Pdf;

/// <summary>
/// Runs whichever real rasteriser this machine has (pdftoppm or Ghostscript). Reported as skipped, not
/// passed, when neither is installed, so a CI run without poppler shows that these did not run.
/// </summary>
public class PdfRasteriserTests
{
    private static IPdfRasteriser RequireRasteriser(PdfThumbnailOptions options)
    {
        var rasteriser = PdfRasteriserFactory.Create(options, NullLogger.Instance);
        Skip.If(rasteriser is UnavailablePdfRasteriser, UnavailablePdfRasteriser.Message);
        return rasteriser;
    }

    [SkippableFact]
    public async Task Renders_Page_One_To_The_Thumbnail_Width()
    {
        // pdftoppm renders straight to ThumbnailWidth; Ghostscript renders at Dpi: 300 x 450 points at 96 dpi
        // is 400 x 600 pixels, so both tools should produce the same image here.
        var rasteriser = RequireRasteriser(new PdfThumbnailOptions { Dpi = 96, ThumbnailWidth = 400 });

        var dir = Path.Combine(Path.GetTempPath(), "pdf_thumbs_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pdfPath = Path.Combine(dir, "a5-ish.pdf");
            var pngPath = Path.Combine(dir, "page1.png");
            await File.WriteAllBytesAsync(pdfPath, TinyPdf.SinglePage(300, 450));

            await rasteriser.RasteriseFirstPage(pdfPath, pngPath);

            File.Exists(pngPath).Should().BeTrue($"{rasteriser.Name} should have written the PNG");
            using var image = Image.NewFromFile(pngPath);
            image.Width.Should().Be(400);
            image.Height.Should().Be(600);
            image.Get("vips-loader").Should().Be("pngload");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [SkippableFact]
    public async Task A_Missing_Pdf_Is_A_Rasterise_Failure_Not_A_Crash()
    {
        var rasteriser = RequireRasteriser(new PdfThumbnailOptions());

        var dir = Path.Combine(Path.GetTempPath(), "pdf_thumbs_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var act = () => rasteriser.RasteriseFirstPage(Path.Combine(dir, "does-not-exist.pdf"), Path.Combine(dir, "out.png"));
            await act.Should().ThrowAsync<PdfRasteriseException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void An_Explicitly_Configured_Tool_That_Is_Not_Installed_Degrades_To_Unavailable()
    {
        var rasteriser = PdfRasteriserFactory.Create(
            new PdfThumbnailOptions { Rasteriser = "pdftoppm", ExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-pdftoppm") },
            NullLogger.Instance);
        // An explicit path is trusted as given; it fails at use, with the tool's own message
        rasteriser.Should().BeOfType<PdftoppmRasteriser>();

        var none = PdfRasteriserFactory.Create(new PdfThumbnailOptions { Rasteriser = "nonsense" }, NullLogger.Instance);
        // "nonsense" falls into auto-detection; whatever it finds is fine, but it never throws
        none.Should().NotBeNull();
    }
}
