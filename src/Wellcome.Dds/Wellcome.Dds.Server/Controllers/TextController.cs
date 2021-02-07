using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils;
using Utils.Storage;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{    
    /// <summary>
    /// Provides raw text from S3, generated from ALTO at workflow processing time.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class TextController : ControllerBase
    {
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;
        private readonly IMetsRepository metsRepository;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="storage">Provides S3 locations</param>
        /// <param name="options">DDS Options</param>
        /// <param name="metsRepository"></param>
        public TextController(
            IStorage storage,
            IOptions<DdsOptions> options,
            IMetsRepository metsRepository
        )
        {
            this.storage = storage;
            ddsOptions = options.Value;
            this.metsRepository = metsRepository;
        }


        /// <summary>
        /// Proxies a raw text blob from S3
        /// </summary>
        /// <param name="id">e.g., b number or manifestation identfier</param>
        /// <returns>A text/plain response, or 404</returns>
        [HttpGet("v1/{id}")]
        public async Task<IActionResult> RawText(string id)
        {
            var stream = await storage.GetStream(ddsOptions.TextContainer, $"raw/{id}");
            if (stream == null)
            {
                return NotFound($"No text resource found for {id}");
            }
            return File(stream, "text/plain");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="manifestationIdentifier"></param>
        /// <param name="assetIdentifier"></param>
        /// <returns></returns>
        [HttpGet("alto/{manifestationIdentifier}/{assetIdentifier}")]
        public async Task<IActionResult> Alto(string manifestationIdentifier, string assetIdentifier)
        {
            var metsManifestation = await metsRepository.GetAsync(manifestationIdentifier) as IManifestation;
            var asset = metsManifestation?.Sequence.SingleOrDefault(pf => pf.StorageIdentifier == assetIdentifier);
            if (asset != null && asset.RelativeAltoPath.HasText())
            {
                var stream = await asset.WorkStore.GetStreamForPathAsync(asset.RelativeAltoPath);
                return File(stream, "text/xml");
            }
            return NotFound($"No ALTO file for {manifestationIdentifier}/{assetIdentifier}");
        }
    }
}