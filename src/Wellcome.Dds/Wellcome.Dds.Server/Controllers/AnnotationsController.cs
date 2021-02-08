using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Storage;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Server.Conneg;

namespace Wellcome.Dds.Server.Controllers
{    
    [Route("[controller]")]
    [ApiController]
    public class AnnotationsController : ControllerBase
    {
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;
        private Helpers helpers;
        private UriPatterns uriPatterns;

        public AnnotationsController(
            IStorage storage,
            IOptions<DdsOptions> options,
            Helpers helpers,
            UriPatterns uriPatterns
        )
        {
            this.storage = storage;
            ddsOptions = options.Value;
            this.helpers = helpers;
            this.uriPatterns = uriPatterns;
        }

        [HttpGet("v3/{*id}")]
        public Task<IActionResult> GetVersion3Annos(string id) => GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        [HttpGet("v2/{identifier}/{assetIdentifier}/line")]
        public Task<IActionResult> GetVersion2CanvasAnnos(string identifier, string assetIdentifier)
        {
            // load the v3 Key
            var key = $"v3/{identifier}/{assetIdentifier}/line";
            var v3 = helpers.LoadAsJson(ddsOptions.AnnotationContainer, key);
            
            // parse and convert to v2
            
            // Jsonify and serve: return Content(..)
        }
        
        [HttpGet("v2/{*id}")]
        public Task<IActionResult> GetVersion2ManifestAnnos(string id) => GetIIIFResource($"v2/{id}", IIIFPresentation.ContentTypes.V2);
        
        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            return await helpers.ServeIIIFContent(ddsOptions.AnnotationContainer, path, contentType, this);
        }
    }
}