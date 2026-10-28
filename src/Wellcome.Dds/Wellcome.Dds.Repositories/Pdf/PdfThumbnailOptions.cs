namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// Configuration section "PdfThumbnails". Every value has a default, so the section may be omitted.
/// </summary>
public class PdfThumbnailOptions
{
    public const string SectionName = "PdfThumbnails";

    /// <summary>
    /// "auto" (pdftoppm if found, otherwise Ghostscript), "pdftoppm" or "ghostscript".
    /// </summary>
    public string Rasteriser { get; set; } = "auto";

    /// <summary>
    /// Full path to the rasteriser executable, when it is not on the PATH (or, for Ghostscript on
    /// Windows, not in the default install location).
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// The rasteriser is killed after this long. Page 1 of a well-formed PDF takes well under a second.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Resolution to render page 1 at before resizing to the thumbnail width. 96 matches the
    /// previous Ghostscript invocation; an A4 page comes out 794 pixels wide.
    /// </summary>
    public int Dpi { get; set; } = 96;

    /// <summary>
    /// PDFs larger than this are not fetched from storage. The whole file has to be downloaded to
    /// render page 1, so this bounds the cost of an on-demand request.
    /// </summary>
    public long MaxPdfBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// Width of the stored thumbnail, which /thumb resizes down from.
    /// </summary>
    public int ThumbnailWidth { get; set; } = 1024;

    public int JpegQuality { get; set; } = 85;
}
