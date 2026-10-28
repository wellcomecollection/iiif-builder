using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Utils;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// The thumbnail the DDS serves for a born-digital PDF: page 1 rendered and resized to
/// <see cref="PdfThumbnailOptions.ThumbnailWidth"/>, stored in the presentation bucket at
/// <c>_pdf_thumbs/{identifier}.jpg</c>. The dashboard's manual upload writes the same key; a generated
/// thumbnail carries S3 metadata saying so, an uploaded one does not, which is how
/// <see cref="PdfThumbnailRefresh.ReplaceGenerated"/> tells them apart.
/// </summary>
public interface IPdfThumbnailService
{
    string GetKey(string identifier);

    /// <summary>
    /// The stored thumbnail, or null if there is none.
    /// </summary>
    Task<Stream?> GetThumbnail(string identifier);

    /// <summary>
    /// Whether a thumbnail is stored. Never throws; an S3 error other than not-found is logged and counts as no thumbnail.
    /// </summary>
    Task<bool> ThumbnailExists(string identifier);

    /// <summary>
    /// Generate and store the thumbnail for a manifestation containing a PDF, subject to <paramref name="refresh"/>.
    /// Never throws for a rasterising failure; see the result. Only PDFs whose access condition allows public
    /// viewing get a thumbnail (see <see cref="PdfThumbnailAccess"/>), because /thumb is public.
    /// </summary>
    Task<PdfThumbnailResult> EnsureThumbnail(DdsIdentity ddsId, PdfThumbnailRefresh refresh = PdfThumbnailRefresh.KeepExisting, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="EnsureThumbnail"/> for every manifestation in a package that contains a PDF. The default
    /// replaces generated thumbnails, since a re-ingested package may contain a changed PDF, but keeps uploaded ones.
    /// </summary>
    Task<List<PdfThumbnailResult>> EnsureThumbnailsForPackage(DdsIdentity packageId, PdfThumbnailRefresh refresh = PdfThumbnailRefresh.ReplaceGenerated, CancellationToken cancellationToken = default);
}

public enum PdfThumbnailRefresh
{
    /// <summary>Do nothing if any thumbnail is stored (generated or uploaded).</summary>
    KeepExisting,
    /// <summary>Regenerate a generated thumbnail; keep one that was uploaded by hand.</summary>
    ReplaceGenerated,
    /// <summary>Regenerate whatever is stored.</summary>
    ReplaceAll
}

public enum PdfThumbnailOutcome
{
    /// <summary>A thumbnail was already stored and the refresh mode kept it.</summary>
    AlreadyExists,
    Generated,
    /// <summary>The manifestation has no PDF file, or the identifier is a collection rather than a manifestation.</summary>
    NoPdf,
    /// <summary>The PDF's access condition does not allow a public thumbnail; see PdfThumbnailAccess.</summary>
    NotPermitted,
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

/// <summary>
/// Which PDFs may have a public thumbnail: the same access conditions that allow an AV poster image
/// to be served (see ThumbController.IsPublicPoster). Page 1 of a closed or restricted PDF is as
/// restricted as the rest of it.
/// </summary>
public static class PdfThumbnailAccess
{
    public const string PdfMimeType = "application/pdf";

    public static readonly string[] PermittedAccessConditions =
    {
        AccessCondition.Open,
        AccessCondition.RequiresRegistration,
        AccessCondition.OpenWithAdvisory
    };

    public static bool IsPermitted(string? accessCondition)
    {
        return accessCondition != null && PermittedAccessConditions.Contains(accessCondition);
    }

    /// <summary>
    /// The first PDF in the manifestation, or null if there is none.
    /// </summary>
    public static IPhysicalFile? GetPdfFile(IManifestation manifestation)
    {
        return manifestation.Sequence.AnyItems().FirstOrDefault(pf => pf.MimeType == PdfMimeType);
    }

    /// <summary>
    /// Whether the manifestation has a PDF whose access condition allows a public thumbnail.
    /// </summary>
    public static bool IsPermitted(IManifestation manifestation)
    {
        return IsPermitted(GetPdfFile(manifestation)?.AccessCondition);
    }
}
