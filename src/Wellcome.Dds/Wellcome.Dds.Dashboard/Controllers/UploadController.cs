using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Aws.S3;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers;

public class UploadController : Controller
{
    // In theory this would be abstracted behind IStorage like our other operations. 
    // But... this is a short term fix, and I'd rather not add write-capability to the IStorage interface.
    private readonly INamedAmazonS3ClientFactory s3ClientFactory;
    private readonly IMetsRepository metsRepository;
    private readonly DdsOptions ddsOptions;

    public UploadController(
        INamedAmazonS3ClientFactory s3ClientFactory,
        IMetsRepository metsRepository,
        IOptions<DdsOptions> ddsOptions)
    {
        this.s3ClientFactory = s3ClientFactory;
        this.metsRepository = metsRepository;
        this.ddsOptions = ddsOptions.Value;
    }

    public async Task<ActionResult> PdfThumb(string id)
    {
        // for safety's sake validate that this is actually a PDF
        var metsResource = await metsRepository.GetAsync(id);
        if (metsResource is IManifestation && metsResource.Type == "Monograph")
        {
            ViewBag.Error = $"{id} is not a valid identifier for pdf thumbnail upload";
        }

        ViewBag.Identifier = id;
        return View();
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
                Key = $"_pdf_thumbs/{id}.jpg",
                BucketName = ddsOptions.PresentationContainer
            };

            var fileTransferUtility = new TransferUtility(s3ClientFactory.Get(NamedClient.Dds));
            await fileTransferUtility.UploadAsync(uploadRequest);
            ViewBag.Message = "Thumbnail uploaded successfully";
        }

        return View();
    }
}