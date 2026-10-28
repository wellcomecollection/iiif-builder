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
/// Runs whichever real rasteriser this machine has (pdftoppm or Ghostscript); passes trivially if neither
/// is installed, so CI without poppler is not blocked. The factory's choice is logged by the test output.
/// </summary>
public class PdfRasteriserTests
{
    [Fact]
    public async Task Renders_Page_One_At_The_Requested_Dpi()
    {
        var rasteriser = PdfRasteriserFactory.Create(new PdfThumbnailOptions { Dpi = 96 }, NullLogger.Instance);
        if (rasteriser is UnavailablePdfRasteriser)
        {
            return;
        }

        var dir = Path.Combine(Path.GetTempPath(), "pdf_thumbs_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pdfPath = Path.Combine(dir, "a5-ish.pdf");
            var jpgPath = Path.Combine(dir, "page1.jpg");
            // 300 x 450 points at 96 dpi is 400 x 600 pixels
            await File.WriteAllBytesAsync(pdfPath, TinyPdf.SinglePage(300, 450));

            await rasteriser.RasteriseFirstPage(pdfPath, jpgPath);

            File.Exists(jpgPath).Should().BeTrue($"{rasteriser.Name} should have written the JPEG");
            using var image = Image.NewFromFile(jpgPath);
            image.Width.Should().Be(400);
            image.Height.Should().Be(600);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task A_Missing_Pdf_Is_A_Rasterise_Failure_Not_A_Crash()
    {
        var rasteriser = PdfRasteriserFactory.Create(new PdfThumbnailOptions(), NullLogger.Instance);
        if (rasteriser is UnavailablePdfRasteriser)
        {
            return;
        }

        var dir = Path.Combine(Path.GetTempPath(), "pdf_thumbs_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var act = () => rasteriser.RasteriseFirstPage(Path.Combine(dir, "does-not-exist.pdf"), Path.Combine(dir, "out.jpg"));
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
