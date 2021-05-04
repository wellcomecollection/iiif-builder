using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement.Mvc;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Utils;
using Utils.Web;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Presentation;

namespace Wellcome.Dds.Server.Controllers
{
    [FeatureGate(FeatureFlags.PresentationServices)]
    [Route("[controller]")]
    public class ThumbController : Controller
    {
        private const int MaxWidth = 1024; 
        private readonly IMetsRepository metsRepository;
        private readonly DdsContext ddsContext;
        private readonly PdfThumbnailUtil pdfThumbnailUtil;
        private readonly ILogger<ThumbController> logger;

        public ThumbController(
            IMetsRepository metsRepository,
            DdsContext ddsContext,
            PdfThumbnailUtil pdfThumbnailUtil,
            ILogger<ThumbController> logger)
        {
            this.metsRepository = metsRepository;
            this.ddsContext = ddsContext;
            this.pdfThumbnailUtil = pdfThumbnailUtil;
            this.logger = logger;
        }

        /// <summary>
        /// Get image representation of specific bnumber id.
        /// </summary>
        /// <param name="id">bnumber to get thumbnail image for</param>
        /// <param name="width">width of requested thumbnail (optional).</param>
        /// <returns>Http response containing thumbnail image</returns>
        [HttpGet("{id}/{width?}")]
        public async Task<IActionResult> Index(string id, int? width = null)
        {
            if (width > MaxWidth)
            {
                return BadRequest($"Requested width exceeds maximum of {MaxWidth}");
            }
            
            var ddsId = new DdsIdentifier(id);
            var manifestations = await GetManifestation(id, ddsId);
            if (!manifestations.Any())
            {
                return NotFound($"No thumbnail for {id}");
            }

            var manifestation = manifestations.First();
            var iiifThumbs = manifestation.GetThumbnail();
            if (iiifThumbs.HasItems())
            {
                return RedirectPermanent(iiifThumbs[0].Id);
            }
            
            if (manifestation.AssetType == "image/jp2")
            {
                return NotFound($"No thumbnail for {id}");
            }

            IMetsResource resource = await metsRepository.GetAsync(id);
            if (manifestations.Exists(m => m.AssetType.StartsWith("audio") || m.AssetType.StartsWith("video")))
            {
                // handle AV request
                return await HandleAvThumbRequest(id, resource, width ?? 600);
            }
            if (manifestations.Exists(m => m.AssetType == "application/pdf"))
            {
                // Born digital PDF...
                return await HandlePdfThumbRequest(id, width ?? 200);
            }
            
            return NotFound($"No thumbnail for {id}");
        }

        // Try to get the specific manifestation, but if that fails, get all for the b number.
        private async Task<List<Manifestation>>GetManifestation(string id, DdsIdentifier ddsId)
        {
            var manifestation = await ddsContext.Manifestations.FindAsync(id);

            // this will also work for plain b number
            if (manifestation == null)
            {
                return ddsContext.Manifestations
                    .Where(m => m.PackageIdentifier == ddsId.BNumber)
                    .ToList();
            }

            return new List<Manifestation>{ manifestation };
        }

        private async Task<IActionResult> HandlePdfThumbRequest(DdsIdentifier identifier, int width)
        {
            Stream thumbnailStream = null;
            try
            {
                thumbnailStream = await pdfThumbnailUtil.GetPdfThumbnail(identifier);
                if (thumbnailStream == null)
                {
                    logger.LogInformation("Request for not found PDF thumbnail for '{Identifier}'", identifier);
                    return NotFound($"No thumbnail image for {identifier}");
                }

                // fail if not there
                if (thumbnailStream.CanSeek)
                {
                    thumbnailStream.Position = 0;
                }

                return await ResizeImageToResponseBody(thumbnailStream, width);
            }
            finally
            {
                thumbnailStream?.Close();
                thumbnailStream?.Dispose();
            }
        }

        private async Task<IActionResult> HandleAvThumbRequest(DdsIdentifier identifier, IMetsResource resource,
            int width)
        {
            if (resource is ICollection multipleManifestation)
            {
                resource = multipleManifestation.Manifestations.FirstOrDefault(
                    mm => mm.Type == "Video" || mm.Type == "Audio");
            }

            if (resource is not IManifestation avManifestation)
            {
                return NotFound($"No poster image for {identifier}");
            }

            Response.CacheForDays(1);
            if (avManifestation.Partial)
            {
                avManifestation = (IManifestation) await metsRepository.GetAsync(avManifestation.Id);
            }

            var poster = avManifestation.PosterImage;
            var isPublicPoster = IsPublicPoster(avManifestation);
            if (poster == null || !isPublicPoster)
            {
                var placeholder = avManifestation.Type == "Audio"
                    ? "/posters/audioplaceholder.png"
                    : "/posters/videoplaceholder.png";
                return Redirect(placeholder);
            }

            await using var imgSourceStream = await poster.WorkStore.GetStreamForPathAsync(poster.RelativePath);
            return await ResizeImageToResponseBody(imgSourceStream, width);
        }

        private static bool IsPublicPoster(IManifestation avManifestation)
        {
            var avFile = avManifestation.Sequence.FirstOrDefault(pf => pf.Family == AssetFamily.TimeBased);
            var permitted = new[]
            {
                AccessCondition.Open,
                AccessCondition.RequiresRegistration,
                AccessCondition.OpenWithAdvisory
            };
            var isPublicPoster = avFile != null && permitted.Contains(avFile.AccessCondition);
            return isPublicPoster;
        }

        private async Task<IActionResult> ResizeImageToResponseBody(Stream imgSourceStream, int width)
        {
            Response.ContentType = "image/jpeg";
            using var image = await Image.LoadAsync(imgSourceStream);
            image.Mutate(x => x.Resize(width, 0, KnownResamplers.Lanczos3));
            await image.SaveAsync(Response.Body, new JpegEncoder());
            return new EmptyResult();
        }
    }
}