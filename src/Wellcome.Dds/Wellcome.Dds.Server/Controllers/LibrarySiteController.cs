using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Utils;
using Wellcome.Dds.Catalogue;
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
        private readonly ICatalogue catalogue;
        private readonly IMemoryCache memoryCache;
        private Helpers helpers;
        
        public WlOrgController(
            UriPatterns uriPatterns,
            IOptions<DdsOptions> options,
            DdsContext ddsContext,
            ICatalogue catalogue,
            IMemoryCache memoryCache,
            Helpers helpers
            )
        {
            this.uriPatterns = uriPatterns;
            this.ddsOptions = options.Value;
            this.ddsContext = ddsContext;
            this.catalogue = catalogue;
            this.memoryCache = memoryCache;
            this.helpers = helpers;
        }
        
        private string Normalise(string bNumber)
        {
            // don't normalise b000-000 forms
            if (bNumber.Contains('-'))
            {
                return bNumber;
            }

            return WellcomeLibraryIdentifiers.GetNormalisedBNumber(bNumber, false);
        }
        
        [HttpGet("iiif/collection/{id}")]
        public IActionResult IIIFCollection(string id)
        {
            return BuilderUrl(uriPatterns.CollectionForWork(Normalise(id)));
        }

     
        [HttpGet("iiif/{id}/manifest")]
        public IActionResult IIIFManifest(string id)
        {
            // We want this to give us the collection URL if it's a collection, not the first manifest.
            // This is because people will ask for this when they don't know in advance it's a collection.
            return ManifestLevelConversion(false, Normalise(id), uriPatterns.Manifest);
        }

        [HttpGet("annoservices/search/{id}")]
        public IActionResult SearchService(string id)
        {
            // This can only work for a manifest, not a collection
            return ManifestLevelConversion(true, id, uriPatterns.IIIFContentSearchService0);
        }
        
        [HttpGet("annoservices/autocomplete/{id}")]
        public IActionResult AutoCompleteService(string id)
        {
            // This can only work for a manifest, not a collection
            return ManifestLevelConversion(true, id, uriPatterns.IIIFAutoCompleteService1);
        }
        
        [HttpGet("player/{id}")]
        [HttpGet("item/{id}")]
        public async Task<IActionResult> ItemPage(string id)
        {
            var work = await catalogue.GetWorkByOtherIdentifier(Normalise(id));
            if (work != null)
            {
                return BuilderUrl(uriPatterns.PersistentPlayerUri(work.Id));
            }
            return NotFound();
        }

        [HttpGet("service/fulltext/{bNumber}/{manifestIndex}")]
        public IActionResult ManifestFullText(string bNumber, int manifestIndex)
        {
            return ManifestLevelConversion(bNumber, manifestIndex, 
                uriPatterns.RawText, string.Empty);
        }
        
        [HttpGet("service/alto/{bNumber}/{manifestIndex}")]
        public async Task<IActionResult> AltoForCanvas(string bNumber, int manifestIndex, int image)
        {
            return await CanvasLevelConversion(bNumber, manifestIndex, image,
                uriPatterns.MetsAlto, string.Empty);
        }
        
        [HttpGet("iiif/{manifestId}/contentAsText/{image}")]
        public async Task<IActionResult> ContentAsText(string manifestId, int image)
        {
            return await CanvasLevelConversionWithVersion(manifestId, image,
                uriPatterns.CanvasOtherAnnotationPageWithVersion, string.Empty);
        }
        
        [HttpGet("iiif/{manifestId}/images")]
        public IActionResult ManifestImageAnnotations(string manifestId)
        {
            return ManifestLevelConversionWithVersion(
                manifestId, uriPatterns.ManifestAnnotationPageImagesWithVersion);
        }
        
        [HttpGet("iiif/{manifestId}/allcontent")]
        public IActionResult ManifestAllContentAnnotations(string manifestId)
        {
            return ManifestLevelConversionWithVersion(
                manifestId, uriPatterns.ManifestAnnotationPageAllWithVersion);
        }
        
        [HttpGet("service/collections")]
        public IActionResult TopLevelCollection()
        {
            return BuilderUrl(uriPatterns.CollectionForAggregation());
        }
        
        [HttpGet("service/collections/archives/lightweight")]
        public IActionResult ArchiveCollectionLightweight()
        {
            return BuilderUrl(uriPatterns.CollectionForAggregation("archives"));
        }
        
        [HttpGet("service/collections/{aggregator}")]
        public IActionResult CollectionAggregation(string aggregator)
        {
            var statusCodeResult = CheckRetiredAggregators(aggregator);
            if (statusCodeResult != null) return statusCodeResult;
            return BuilderUrl(uriPatterns.CollectionForAggregation(GetNewAggregator(aggregator)));
        }


        [HttpGet("service/collections/archives/{*referenceNumber}")]
        public IActionResult CollectionArchiveRefNumber(string referenceNumber)
        {
            return BuilderUrl(uriPatterns.CollectionForAggregation("archives", referenceNumber));
        }

        
        [HttpGet("service/collections/{aggregator}/{value}")]
        public IActionResult CollectionAggregation(string aggregator, string value)
        {
            var statusCodeResult = CheckRetiredAggregators(aggregator);
            if (statusCodeResult != null) return statusCodeResult;
            return BuilderUrl(uriPatterns.CollectionForAggregation(GetNewAggregator(aggregator), value));
        }

        [HttpGet("service/workthumb/{bNumber}/{manifestIndex}")]
        public IActionResult WorkThumb(string bNumber, int manifestIndex)
        {
            return ManifestLevelConversion(bNumber, manifestIndex, 
                uriPatterns.WorkThumbnail, string.Empty);
        }
        
        [HttpGet("service/playerconfig")]
        public IActionResult PlayerConfig()
        {
            return BuilderUrl(uriPatterns.Path("/service/playerconfig"));
        }

        /**
         * #################################### MOH ######################################
         */

        [HttpGet("service/moh/normalisednames")]
        public IActionResult MoHNormalisedNames()
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/allplacenames")]
        public IActionResult MoHAllPlaceNames()
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/normalisedmohplacenames ")]
        public IActionResult MoHNormalisedMoHPlaceNames()
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/browseanyplace")]
        public IActionResult MoHBrowseAnyPlace(int pageSize = 0)
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/autocompleteplace")]
        public IActionResult MoHAutoCompletePlace(string fragment)
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/browsenormalised")]
        public IActionResult MoHBrowseNormalised(
            string normalisedPlace = null, 
            int startYear = 1848, int endYear = 2020, 
            int page = 1, int pageSize = 100) //, 
            // Ordering ordering = Ordering.Date) TODO - put this back in!
        {
            return new StatusCodeResult(501);
        }
        
        
        [HttpGet("service/moh/browseanyplace")]
        public IActionResult MoHBrowseAnyPlace(
            string place = null,
            int startYear = 1848, int endYear = 2020,
            int page = 1, int pageSize = 100) //, 
            // Ordering ordering = Ordering.Date) TODO - put this back in!
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/report/{id}")]
        public IActionResult MoHReport(string id)
        {
            return new StatusCodeResult(501);   
        }
        
                
        [HttpGet("service/moh/report/{id}/{page}")]
        public IActionResult MoHReportPage(string id, int page)
        {
            return new StatusCodeResult(501);
        }
                
        [HttpGet("service/moh/search")]
        public IActionResult MoHSearch(            
            string terms,
            string constrainedPlaceName = null,
            int startYear = 1848, int endYear = 2020,
            int page = 1, int pageSize = 100,
            bool tablesOnly = false, bool groupIntoReports = true,
            bool useSpecialGroupingBehaviour = true, int visibleHitsPerReport = 5, int expandoThreshold = 2)
        {
            return new StatusCodeResult(501);
        }

        [HttpGet("service/moh/tables/{tableId}.{fileExt}")]
        public IActionResult MoHTables(long tableId, string fileExt)
        {
            return new StatusCodeResult(501);
        }
        
        [HttpGet("service/moh/zip")]
        public IActionResult MoHZip(
            string op, string format, int startYear = 0, int endYear = 2020,
            bool useNormalisedPlace = false)
        {
            return new StatusCodeResult(501);
        }
        
        private string GetNewAggregator(string oldAggregator)
        {
            return oldAggregator switch
            {
                "topics" => "subjects",
                "authors" => "contributors",
                "collections" => "digitalcollections",
                _ => oldAggregator
            };
        }
        
        private StatusCodeResult CheckRetiredAggregators(string aggregator)
        {
            if (aggregator == "access")
            {
                return new StatusCodeResult(501);
            }
            if (aggregator == "fulltext")
            {
                return new StatusCodeResult(501);
            }
            if (aggregator == "recently-digitised")
            {
                return new StatusCodeResult(501);
            }
            if (aggregator == "lucky-dip")
            {
                return new StatusCodeResult(501);
            }

            return null;
        }

        
        private IActionResult ManifestLevelConversion(bool useFirstManifestation, string id, Func<string, string> converter)
        {
            var idParts = ManifestIdParts(id, -1);
            if (idParts.index == -1)
            {
                if (useFirstManifestation == false)
                {
                    // Use the b number as-is, don't try to deduce a manifest ID
                    return BuilderUrl(converter(idParts.bNumber));
                }

                idParts.index = 0;
            }
            var manifestation = ddsContext.GetManifestationByIndex(idParts.bNumber, idParts.index);
            if (manifestation == null)
            {
                return NotFound();
            }
            return BuilderUrl(converter(manifestation.Id));
        }
        
        private IActionResult ManifestLevelConversionWithVersion(string id, Func<string, int, string> converter)
        {
            var idParts = ManifestIdParts(id, 0);
            var manifestation = ddsContext.GetManifestationByIndex(idParts.bNumber, idParts.index);
            if (manifestation == null)
            {
                return NotFound();
            }
            return BuilderUrl(converter(manifestation.Id, 2));
        }
        
        private IActionResult ManifestLevelConversion(string bNumber, int manifestIndex,
            Func<string, string> converter, string newQueryString)
        {
            var manifestation = ddsContext.GetManifestationByIndex(bNumber, manifestIndex);
            if (manifestation == null)
            {
                return NotFound();
            }
            return BuilderUrl(converter(manifestation.Id), newQueryString);
        }

        private async Task<IActionResult> CanvasLevelConversion(string bNumber, int manifestIndex, int canvasIndex,
            Func<string, string, string> converter, string newQueryString)
        {
            var manifestation = ddsContext.GetManifestationByIndex(bNumber, manifestIndex);
            if (manifestation == null)
            {
                return NotFound();
            }
            var canvasPart = await GetCanvasPart(canvasIndex, manifestation);
            return BuilderUrl(converter(manifestation.Id, canvasPart), newQueryString);
        }
        
        private async Task<IActionResult> CanvasLevelConversion(string manifestId, int canvasIndex,
            Func<string, string, string> converter, string newQueryString)
        {
            var idParts = ManifestIdParts(manifestId, 0);
            var manifestation = ddsContext.GetManifestationByIndex(idParts.bNumber, idParts.index);
            if (manifestation == null)
            {
                return NotFound();
            }
            var canvasPart = await GetCanvasPart(canvasIndex, manifestation);
            return BuilderUrl(converter(manifestation.Id, canvasPart), newQueryString);
        }
        
        private async Task<IActionResult> CanvasLevelConversionWithVersion(string manifestId, int canvasIndex,
            Func<string, string, int, string> converter, string newQueryString)
        {
            var idParts = ManifestIdParts(manifestId, 0);
            var manifestation = ddsContext.GetManifestationByIndex(idParts.bNumber, idParts.index);
            if (manifestation == null)
            {
                return NotFound();
            }
            var canvasPart = await GetCanvasPart(canvasIndex, manifestation);
            return BuilderUrl(converter(manifestation.Id, canvasPart, 2), newQueryString);
        }

        private async Task<string> GetCanvasPart(int canvasIndex, Manifestation manifestation)
        {
            var key = $"canvas_indexes_{manifestation.Id}";
            var canvasIndexes = memoryCache.Get<Dictionary<int, string>>(key);
            if (canvasIndexes == null)
            {
                canvasIndexes = await MakeCanvasIndexes(manifestation.Id);
                memoryCache.Set(key, canvasIndexes);
            }

            var canvasPart = canvasIndexes[canvasIndex];
            return canvasPart;
        }

        private async Task<Dictionary<int, string>> MakeCanvasIndexes(string manifestationId)
        {
            // what's the best way to find the canvas IDs for a given manifest?
            // We already have the means of reading from storage, storage maps, etc.
            // But that's quite heavy and depends on the total size of the work.
            // This version loads the already-created MANIFEST and finds the canvasIDs
            // from that.
            var s3Key = $"v3/{manifestationId}";
            var jManifest = await helpers.LoadAsJson(ddsOptions.PresentationContainer, s3Key);
            var dict = new Dictionary<int, string>();
            if (jManifest != null)
            {
                var items = jManifest["items"] as JArray;
                for (int i = 0; i < items.Count; i++)
                {
                    var assetPart = items[i]["id"].Value<string>().Split("/canvases/").Last();
                    dict.Add(i, assetPart);
                }
            }

            return dict;
        }


        private (string bNumber, int index) ManifestIdParts(string id, int defaultIndex)
        {
            string bNumber;
            int index;
            if (id.Contains('-'))
            {
                // A multiple-manifestation volume
                var parts = id.Split('-');
                bNumber = parts[0];
                index = int.Parse(parts[1]);
            }
            else
            {
                bNumber = id;
                index = defaultIndex; // You'll need to do further work to know whether bNumber is a Collection
            }

            return (bNumber, index);
        }


        /// <summary>
        /// Redirect to new URL, or return new URL, depending on which path you came in on.
        /// Try to preserve the querystring, unless a new querystring has been provided.
        /// </summary>
        /// <param name="newUrl"></param>
        /// <param name="updatedQueryString"></param>
        /// <returns></returns>
        [HttpGet]
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
            var versioned = newUrl.ReplaceFirst("/presentation/", "/presentation/v2/");
            
            if (Request.Path.StartsWithSegments("/wlorgr"))
            {
                return RedirectPermanent(versioned);
            }


            return Content(versioned, "text/plain");
        }
    }
}
#pragma warning restore 1591