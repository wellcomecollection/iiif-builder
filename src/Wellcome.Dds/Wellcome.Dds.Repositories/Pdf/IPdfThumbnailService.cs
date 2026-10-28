using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// The thumbnail the DDS serves for a born-digital PDF: page 1 rendered and resized to
/// <see cref="PdfThumbnailOptions.ThumbnailWidth"/>, stored in the presentation bucket at
/// <c>_pdf_thumbs/{identifier}.jpg</c>. The dashboard's manual upload writes the same key, so an
/// uploaded thumbnail overrides a generated one until it is regenerated.
/// </summary>
public interface IPdfThumbnailService
{
    string GetKey(string identifier);

    /// <summary>
    /// The stored thumbnail, or null if there is none.
    /// </summary>
    Task<Stream?> GetThumbnail(string identifier);

    Task<bool> ThumbnailExists(string identifier);

    /// <summary>
    /// Generate and store the thumbnail for a manifestation containing a PDF, unless one exists already
    /// (or <paramref name="regenerate"/> is set). Never throws for a rasterising failure; see the result.
    /// </summary>
    Task<PdfThumbnailResult> EnsureThumbnail(DdsIdentity ddsId, bool regenerate = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="EnsureThumbnail"/> for every manifestation in a package that contains a PDF.
    /// </summary>
    Task<List<PdfThumbnailResult>> EnsureThumbnailsForPackage(DdsIdentity packageId, bool regenerate = false, CancellationToken cancellationToken = default);
}

public enum PdfThumbnailOutcome
{
    /// <summary>A thumbnail was already stored and regenerate was not requested.</summary>
    AlreadyExists,
    Generated,
    /// <summary>The manifestation has no PDF file, or the identifier is a collection rather than a manifestation.</summary>
    NoPdf,
    /// <summary>The PDF exceeds PdfThumbnailOptions.MaxPdfBytes.</summary>
    TooLarge,
    /// <summary>Fetching, rendering, resizing or storing failed; Message says which.</summary>
    Failed
}

public class PdfThumbnailResult
{
    public PdfThumbnailResult(string identifier, string key, PdfThumbnailOutcome outcome, string? message = null)
    {
        Identifier = identifier;
        Key = key;
        Outcome = outcome;
        Message = message;
    }

    public string Identifier { get; }
    public string Key { get; }
    public PdfThumbnailOutcome Outcome { get; }
    public string? Message { get; }

    /// <summary>
    /// The PDF's path within the package, when one was found.
    /// </summary>
    public string? SourcePath { get; set; }

    public bool ThumbnailAvailable => Outcome is PdfThumbnailOutcome.AlreadyExists or PdfThumbnailOutcome.Generated;

    public override string ToString()
    {
        return $"{Identifier}: {Outcome}{(Message == null ? "" : " - " + Message)}";
    }
}
