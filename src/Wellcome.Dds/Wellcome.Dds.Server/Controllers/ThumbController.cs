using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Utils;
using Utils.Web;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Presentation;

namespace Wellcome.Dds.Server.Controllers
{
    public class ThumbController : Controller
    {
        private readonly IMetsRepository metsRepository;
        private DdsContext ddsContext;
        private UriPatterns uriPatterns;
        
        public ThumbController(
            IMetsRepository metsRepository,
            DdsContext ddsContext,
            UriPatterns uriPatterns)
        {
            this.metsRepository = metsRepository;
            this.ddsContext = ddsContext;
            this.uriPatterns = uriPatterns;
        }
        
        [Route("thumb/{id}")]
        [HttpGet]
        public async Task<IActionResult> Index(string id)
        {
            // TODO - if it's a BD PDF, return the large thumb or a resize of it or an info.json
            var ddsId = new DdsIdentifier(id);
            var manifestation = await ddsContext.Manifestations.FindAsync(id);
            // this will also work for plain b number
            if (manifestation == null)
            {
                manifestation = ddsContext.Manifestations
                    .FirstOrDefault(m => m.PackageIdentifier == ddsId.BNumber);
            }

            if (manifestation == null)
            {
                return NotFound($"No thumbnail for {id}");
            }

            var iiifThumbs = manifestation.GetThumbnail();
            if (iiifThumbs.HasItems())
            {
                return RedirectPermanent(iiifThumbs[0].Id);
            }
            
            // TODO - PDF Cover thumbs - use Cantaloupe!
            if (manifestation.AssetType == "image/jp2")
            {
                return NotFound($"No thumbnail for {id}");
            }
            
            IMetsResource resource = await metsRepository.GetAsync(id);
            if (resource is ICollection multipleManifestation)
            {
                resource = multipleManifestation.Manifestations.FirstOrDefault(
                    mm => mm.Type == "Video" || mm.Type == "Audio");
            }
            var avManifestation = resource as IManifestation;
            if (avManifestation == null)
            {
                return NotFound($"No poster image for {id}");
            }

            Response.CacheForDays(1);
            if (avManifestation.Partial)
            {
                avManifestation = (IManifestation) await metsRepository.GetAsync(avManifestation.Id);
            }
            var poster = avManifestation.PosterImage;
            var avFile = avManifestation.Sequence.FirstOrDefault(pf => pf.Family == AssetFamily.TimeBased);
            var permitted = new []
            {
                AccessCondition.Open,
                AccessCondition.RequiresRegistration,
                AccessCondition.OpenWithAdvisory
            };
            var isPublicPoster = avFile != null && permitted.Contains(avFile.AccessCondition);
            if (poster == null || !isPublicPoster)
            {
                var placeholder = avManifestation.Type == "Audio"
                    ? "/posters/audioplaceholder.png"
                    : "/posters/videoplaceholder.png";
                return Redirect(placeholder);
            }

            Response.ContentType = "image/jpeg";
            var imgSourceStream = await poster.WorkStore.GetStreamForPathAsync(poster.RelativePath);
            using (var image = await Image.LoadAsync(imgSourceStream))
            {
                image.Mutate(x => x.Resize(600, 0, KnownResamplers.Lanczos3));
                await image.SaveAsync(Response.Body, new JpegEncoder());
            }

            return new EmptyResult(); // Is this right?
        }
    }
}