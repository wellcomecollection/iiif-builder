using System.Threading.Tasks;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Server.Conneg;

namespace Wellcome.Dds.Server.Controllers
{    
    [FeatureGate(FeatureFlags.PresentationServices)]
    [Route("[controller]")]
    [ApiController]
    public class AnnotationsController : ControllerBase
    {
        private readonly DdsOptions ddsOptions;
        private Helpers helpers;
        private IIIIFBuilder iiifBuilder;

        public AnnotationsController(
            IOptions<DdsOptions> options,
            Helpers helpers,
            IIIIFBuilder iiifBuilder
        )
        {
            ddsOptions = options.Value;
            this.helpers = helpers;
            this.iiifBuilder = iiifBuilder;
        }

        [HttpGet("v3/{*id}")]
        public Task<IActionResult> GetVersion3Annos(string id) => 
            GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        [HttpGet("v2/{identifier}/{assetIdentifier}/line")]
        public async Task<IActionResult> GetVersion2CanvasAnnos(string identifier, string assetIdentifier)
        {
            // load the v3 Key
            var key = $"v3/{identifier}/{assetIdentifier}/line";
            var v3 = await helpers.LoadAsJson(ddsOptions.AnnotationContainer, key);

            if (v3 == null)
            {
                return NotFound($"No annotation page available for {identifier}/{assetIdentifier}.");
            }
            
            // parse and convert to v2
            var v2 = iiifBuilder.ConvertW3CAnnoPageJsonToOAAnnoList(
                v3, identifier, assetIdentifier);
            
            // Jsonify and serve: return Content(..)
            return Content(v2.AsJson(), IIIFPresentation.ContentTypes.V2);
        }
        
        [HttpGet("v2/{*id}")]
        public Task<IActionResult> GetVersion2ManifestAnnos(string id) => 
            GetIIIFResource($"v2/{id}", IIIFPresentation.ContentTypes.V2);
        
        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            return await helpers.ServeIIIFContent(ddsOptions.AnnotationContainer, path, contentType, this);
        }
    }
}