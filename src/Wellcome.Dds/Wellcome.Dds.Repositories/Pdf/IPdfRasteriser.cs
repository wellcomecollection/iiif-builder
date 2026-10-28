using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// Renders the first page of a PDF to a PNG file. Implemented by external tools run as a
/// subprocess (poppler's pdftoppm, Ghostscript), so a malformed PDF can at worst kill that process.
/// </summary>
public interface IPdfRasteriser
{
    /// <summary>
    /// Which tool this is, for logs and the dashboard.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Render page 1 of the PDF at <paramref name="pdfPath"/> to <paramref name="outputPngPath"/>, at
    /// (or above) the thumbnail width the rasteriser was created with.
    /// </summary>
    /// <exception cref="PdfRasteriseException">The tool failed, timed out, or produced no output.</exception>
    Task RasteriseFirstPage(string pdfPath, string outputPngPath, CancellationToken cancellationToken = default);
}

public class PdfRasteriseException : Exception
{
    public PdfRasteriseException(string message) : base(message)
    {
    }

    public PdfRasteriseException(string message, Exception inner) : base(message, inner)
    {
    }
}
