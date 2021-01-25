using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Utils.Web;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{
    public class PosterImageController : Controller
    {
        private readonly IMetsRepository metsRepository;
        
        public PosterImageController(IMetsRepository metsRepository)
        {
            this.metsRepository = metsRepository;
        }
        
        [Route("thumbs/{id}")]
        public async Task<IActionResult> Index(string id)
        {
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
            var permitted = new [] {AccessCondition.Open, AccessCondition.RequiresRegistration};
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