using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Utils;
using Utils.Storage;
using Utils.Web;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{    
    /// <summary>
    /// Provides raw text from S3, generated from ALTO at workflow processing time.
    /// </summary>
    [FeatureGate(FeatureFlags.TextServices)]
    [Route("[controller]")]
    [ApiController]
    public class TextController : ControllerBase
    {
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;
        private readonly IMetsRepository metsRepository;
        private readonly ILogger<TextController> logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="storage">Provides S3 locations</param>
        /// <param name="options">DDS Options</param>
        /// <param name="metsRepository"></param>
        public TextController(
            IStorage storage,
            IOptions<DdsOptions> options,
            IMetsRepository metsRepository,
            ILogger<TextController> logger
        )
        {
            this.storage = storage;
            ddsOptions = options.Value;
            this.metsRepository = metsRepository;
            this.logger = logger;
        }

        /// <summary>
        /// Proxies a raw text blob from S3
        /// </summary>
        /// <param name="id">e.g., b number or manifestation identifier</param>
        /// <returns>A text/plain response, or 404</returns>
        [HttpGet("v1/{id}")]
        public async Task<IActionResult> RawText(string id)
        {
            var stream = await GetTextObjectStream($"raw/{id}");
            if (stream == null)
            {
                return NotFound($"No text resource found for {id}");
            }
            
            Response.CacheForDays(1);
            return File(stream, "text/plain");
        }

        /// <summary>
        /// Get all text resources in compressed zip file.
        /// </summary>
        /// <param name="id">b number or manifestation identifier</param>
        /// <returns>application/zip response, or 404 if not found</returns>
        [HttpGet("v1/{id}.zip")]
        public async Task<IActionResult> TextZip(string id)
        {
            var zipStream = await GetTextObjectStream($"zip/{id}.zip");
            if (zipStream == null)
            {
                return NotFound($"No text resource found for {id}.zip");
            }
            
            return File(zipStream, "application/zip", $"{id}-full-text.zip");
        }

        /// <summary>
        /// Get ALTO xml file for specified identifier.
        /// </summary>
        /// <param name="manifestationIdentifier">manifestation identifier</param>
        /// <param name="assetIdentifier">asset identifier</param>
        /// <returns>text/xml response, or 404 if not found</returns>
        [HttpGet("alto/{manifestationIdentifier}/{assetIdentifier}")]
        public async Task<IActionResult> Alto(string manifestationIdentifier, string assetIdentifier)
        {
            var metsManifestation = await metsRepository.GetAsync(manifestationIdentifier) as IManifestation;
            var asset = metsManifestation?.Sequence.SingleOrDefault(pf => pf.StorageIdentifier == assetIdentifier);
            if (asset != null && asset.RelativeAltoPath.HasText())
            {
                try
                {
                    var stream = await asset.WorkStore.GetStreamForPathAsync(asset.RelativeAltoPath);
                    return File(stream, "text/xml");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Cannot stream ALTO: {manifestationIdentifier}/{assetIdentifier}", 
                        manifestationIdentifier, assetIdentifier);
                    return Problem("Unable to stream expected ALTO file from S3", 
                        null, 500, "Stream error", null);
                }
            }
            return NotFound($"No ALTO file for {manifestationIdentifier}/{assetIdentifier}");
        }

        private async Task<Stream> GetTextObjectStream(string key)
            => await storage.GetStream(ddsOptions.TextContainer, key);
    }
}