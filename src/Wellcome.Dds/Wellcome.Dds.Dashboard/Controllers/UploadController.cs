using System.Threading.Tasks;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Aws.S3;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Pdf;

namespace Wellcome.Dds.Dashboard.Controllers;

/// <summary>
/// The thumbnail a born-digital PDF shows in viewers (/thumb/{id}) is page 1 of the PDF, stored at
/// _pdf_thumbs/{id}.jpg. It is generated at ingest and on demand; this page regenerates it, or lets
/// a hand-made image be uploaded in its place when page 1 is a poor choice (a blank cover, say).
/// </summary>
public class UploadController : Controller
{
    // In theory, this would be abstracted behind IStorage like our other operations.
    // But... this is a short term fix, and I'd rather not add write-capability to the IStorage interface.
    private readonly INamedAmazonS3ClientFactory s3ClientFactory;
    private readonly IMetsRepository metsRepository;
    private readonly DdsOptions ddsOptions;
    private readonly IIdentityService identityService;
    private readonly IPdfThumbnailService pdfThumbnailService;

    public UploadController(
        INamedAmazonS3ClientFactory s3ClientFactory,
        IMetsRepository metsRepository,
        IOptions<DdsOptions> ddsOptions,
        IIdentityService identityService,
        IPdfThumbnailService pdfThumbnailService)
    {
        this.s3ClientFactory = s3ClientFactory;
        this.metsRepository = metsRepository;
        this.ddsOptions = ddsOptions.Value;
        this.identityService = identityService;
        this.pdfThumbnailService = pdfThumbnailService;
    }

    public async Task<ActionResult> PdfThumb(string id)
    {
        var ddsId = identityService.GetIdentity(id);
        await PopulateViewBag(ddsId);
        return View();
    }

    /// <summary>
    /// The stored thumbnail itself, for the preview on the page (the dashboard has no public /thumb).
    /// </summary>
    public async Task<ActionResult> PdfThumbImage(string id)
    {
        var ddsId = identityService.GetIdentity(id);
        var stream = await pdfThumbnailService.GetThumbnail(ddsId.Value);
        if (stream == null)
        {
            return NotFound();
        }
        Response.Headers["Cache-Control"] = "no-store";
        return File(stream, "image/jpeg");
    }

    [HttpPost]
    public async Task<ActionResult> GeneratePdfThumb(string id)
    {
        var ddsId = identityService.GetIdentity(id);
        var result = await pdfThumbnailService.EnsureThumbnail(ddsId, PdfThumbnailRefresh.ReplaceAll, HttpContext.RequestAborted);
        if (result.Outcome == PdfThumbnailOutcome.Generated)
        {
            ViewBag.Message = $"Generated {result.Message} from {result.SourcePath}";
        }
        else
        {
            ViewBag.Error = result.ToString();
        }
        await PopulateViewBag(ddsId);
        return View("PdfThumb");
    }

    public async Task<ActionResult> UploadedPdfThumb(string id, [FromForm(Name ="thumbFile")] IFormFile thumb)
    {
        if (thumb == null || thumb.Length == 0)
        {
            ViewBag.Error = "No file supplied.";
        }
        else
        {
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = thumb.OpenReadStream(),
                Key = pdfThumbnailService.GetKey(id),
                BucketName = ddsOptions.PresentationContainer,
                ContentType = "image/jpeg"
            };
            uploadRequest.Metadata.Add(PdfThumbnailService.SourceMetadataKey, PdfThumbnailService.SourceMetadataUploaded);

            var fileTransferUtility = new TransferUtility(s3ClientFactory.Get(NamedClient.Dds));
            await fileTransferUtility.UploadAsync(uploadRequest);
            ViewBag.Message = "Thumbnail uploaded successfully";
        }

        await PopulateViewBag(identityService.GetIdentity(id));
        return View("PdfThumb");
    }

    private async Task PopulateViewBag(DdsIdentity ddsId)
    {
        ViewBag.Identifier = ddsId.Value;
        ViewBag.ThumbKey = pdfThumbnailService.GetKey(ddsId.Value);
        ViewBag.HasThumbnail = await pdfThumbnailService.ThumbnailExists(ddsId.Value);
        // for safety's sake validate that this is actually a PDF
        var metsResource = await metsRepository.GetAsync(ddsId);
        if (metsResource is IManifestation && metsResource.Type == "Monograph")
        {
            ViewBag.Error ??= $"{ddsId.Value} is not a valid identifier for pdf thumbnail upload";
        }
    }
}
