using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;

#pragma warning disable 1591
namespace Wellcome.Dds.Server.Controllers
{
    /// <summary>
    /// Provides paths on iiif.wellcomecollection.org, for paths on wellcomelibrary.org.
    /// </summary>
    [Route("wlorgp")]
    [Route("wlorgr")]
    [ApiController]
    public class WlOrgController : ControllerBase
    {
        private readonly DdsOptions ddsOptions;
        private readonly UriPatterns uriPatterns;
        private readonly DdsContext ddsContext;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uriPatterns"></param>
        /// <param name="options"></param>
        /// <param name="ddsContext"></param>
        public WlOrgController(
            UriPatterns uriPatterns,
            IOptions<DdsOptions> options,
            DdsContext ddsContext
            )
        {
            this.uriPatterns = uriPatterns;
            this.ddsOptions = options.Value;
            this.ddsContext = ddsContext;
        }

        
        [HttpGet("iiif/collection/{id}")]
        public IActionResult IIIFCollection(string id)
        {
            return BuilderUrl(uriPatterns.CollectionForWork(id));
        }
        
        [HttpGet("iiif/{id}/manifest")]
        public IActionResult IIIFManifest(string id)
        {
            if (id.Contains('-'))
            {
                var manifestation = GetManifestationFromTwoPartId(id);
                if (manifestation == null)
                {
                    return NotFound();
                }
                return BuilderUrl(uriPatterns.Manifest(manifestation.Id));
            }
            // A regular single manifest
            return BuilderUrl(uriPatterns.Manifest(id));
        }

        [HttpGet("annoservices/search/{id}")]
        public IActionResult SearchService(string id)
        {
            if (id.Contains('-'))
            {
                var manifestation = GetManifestationFromTwoPartId(id);
                if (manifestation == null)
                {
                    return NotFound();
                }
                return BuilderUrl(uriPatterns.IIIFContentSearchService0(manifestation.Id));
            }
            return BuilderUrl(uriPatterns.IIIFContentSearchService0(id));
        }

        private Manifestation GetManifestationFromTwoPartId(string id)
        {
            // A multiple-manifestation volume
            var parts = id.Split('-');
            var bNumber = parts[0];
            var index = int.Parse(parts[1]);
            var manifestation = ddsContext.GetManifestationByIndex(bNumber, index);
            return manifestation;
        }


        /// <summary>
        /// Redirect to new URL, or return new URL, depending on which path you came in on.
        /// Try to preserve the querystring, unless a new querystring has been provided.
        /// </summary>
        /// <param name="newUrl"></param>
        /// <param name="updatedQueryString"></param>
        /// <returns></returns>
        public IActionResult BuilderUrl(string newUrl, string updatedQueryString = null)
        {
            if (updatedQueryString != null)
            {
                newUrl += updatedQueryString;
            } 
            else if (Request.QueryString.HasValue)
            {
                newUrl += Request.QueryString;
            }
            if (Request.Path.StartsWithSegments("/wlorgr"))
            {
                return RedirectPermanent(newUrl);
            }

            return Content(newUrl, "text/plain");
        }
    }
}
#pragma warning restore 1591