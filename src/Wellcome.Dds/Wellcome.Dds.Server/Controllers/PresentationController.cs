using System.Threading.Tasks;
using IIIF.Presentation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils.Storage;
using Wellcome.Dds.Common;
using Wellcome.Dds.Server.Conneg;

namespace Wellcome.Dds.Server.Controllers
{
    /// <summary>
    /// Mostly now just a Proxy to S3 resources made by WorkflowProcessor.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class PresentationController : ControllerBase
    {
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;
        private Helpers helpers;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="options"></param>
        /// <param name="helpers"></param>
        public PresentationController(
            IStorage storage,
            IOptions<DdsOptions> options,
            Helpers helpers
            )
        {
            this.storage = storage;
            ddsOptions = options.Value;
            this.helpers = helpers;
        }
        
        /// <summary>
        /// The canonical route for IIIF resources.
        /// Supports content negotiation.
        ///
        /// This is simply a proxy to resources in S3.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")] 
        public Task<IActionResult> Index(string id)
        {
            // Return requested version if headers present, or fallback to known version
            var iiifVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType(Version.V3);
            return iiifVersion == Version.V2 ? V2(id) : V3(id);
        }

        /// <summary>
        /// Non conneg explicit path for IIIF 2.1
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v2/{id}")]
        public Task<IActionResult> V2(string id) => GetIIIFResource($"v2/{id}", IIIFPresentation.ContentTypes.V2);

        /// <summary>
        /// Non conneg explicit path for IIIF 3.0
        /// </summary>
        /// <param name="id">The resource identifier</param>
        /// <returns></returns>
        [HttpGet("v3/{id}")]
        public Task<IActionResult> V3(string id) => GetIIIFResource($"v3/{id}", IIIFPresentation.ContentTypes.V3);

        private async Task<IActionResult> GetIIIFResource(string path, string contentType)
        {
            return await helpers.ServeIIIFContent(ddsOptions.PresentationContainer, path, contentType, this);
        }
    }
}