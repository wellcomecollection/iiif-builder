using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Storage;
using Wellcome.Dds.Common;
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

        public AnnotationsController(
            IStorage storage,
            IOptions<DdsOptions> options,
            Helpers helpers
        )
        {
            this.storage = storage;
            ddsOptions = options.Value;
            this.helpers = helpers;
        }

        // TODO: What is the correct content type for W3C Web annos?
        [HttpGet("v3/{*id}")]
        public Task<IActionResult> GetVersion3Annos(string id) => GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        // TODO: What do we do for the V2 path?
        // either... emit at creation time (=> 2x the resources in S3)
        // or convert dynamically (=> computational overhead when harvested)
        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            return await helpers.ServeIIIFContent(ddsOptions.AnnotationContainer, path, contentType, this);
        }
    }
}