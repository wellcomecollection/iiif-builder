using System.Collections.Generic;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Server.Models;

namespace Wellcome.Dds.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class PresentationController : ControllerBase
    {
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;

        public PresentationController(
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DlcsOptions> dlcsOptions)
        {
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.dlcsOptions = dlcsOptions.Value;
        }
        
        [HttpGet("{id}")] 
        public async Task<IIIFPrecursor> Index(string id)
        {
            // No error handling in here at all for demo
            var ddsId = new DdsIdentifier(id);
            var workTask = catalogue.GetWork(ddsId.BNumber);
            var ddsTask = dashboardRepository.GetDigitisedResource(id);
            await Task.WhenAll(new List<Task> {ddsTask, workTask});

            var digitisedResource = ddsTask.Result;
            var work = workTask.Result;
            CleanManifestation(digitisedResource);
            var model = new IIIFPrecursor
            {
                Id = id,
                CatalogueMetadata = work,
                Label = digitisedResource.BNumberModel.DisplayTitle,
                Comment = $"This is a {digitisedResource.GetType()}",
                ManifestSource = digitisedResource as DigitisedManifestation,
                SimpleCollectionSource = SimpleCollectionModel.MakeSimpleCollectionModel(digitisedResource as IDigitisedCollection),
                Pdf = string.Format(dlcsOptions.SkeletonNamedPdfTemplate, dlcsOptions.CustomerDefaultSpace, id)
            };
            return model;
        }

        [HttpGet("v2/{id}")]
        public ActionResult V2(string id)
        {
            var acceptHeader = Request.GetTypedHeaders().Accept;
            return Ok("V2");
        }
        
        [HttpGet("v3/{id}")]
        public ActionResult V3(string id)
        {
            var acceptHeader = Request.GetTypedHeaders().Accept;
            return Ok("V3");
        }
        
        // /b12345678 returns 3.0, unless conneg header specifies. return header to canonical version
        // Accept: application/ld+json;profile=http://iiif.io/api/presentation/3/context.json
        // /v2/b12345678 returns 2.1
        // /v3/b12345678 returns 3.0

        private void CleanManifestation(IDigitisedResource manifestation)
        {
            if (manifestation is DigitisedManifestation)
            {
                var metsManifestation = ((DigitisedManifestation) manifestation).MetsManifestation; 
                // remove refs that just clutter up the JSON
                metsManifestation.Sequence = null;
                foreach (var physicalFile in metsManifestation.SignificantSequence)
                {
                    physicalFile.WorkStore = null;
                }
            }
        }
    }
}