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
using Wellcome.Dds.Server.Conneg;
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
        public Task<IIIFPrecursor> Index(string id)
        {
            // Return requested version if headers present, or fallback to known version
            var iiifVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType(IIIF.Presentation.Version.V3);
            return CreateIIIFPrecursor(id, iiifVersion);
        }

        [HttpGet("v2/{id}")]
        public Task<IIIFPrecursor> V2(string id) => CreateIIIFPrecursor(id, IIIF.Presentation.Version.V2);

        [HttpGet("v3/{id}")]
        public Task<IIIFPrecursor> V3(string id) => CreateIIIFPrecursor(id, IIIF.Presentation.Version.V3);

        private async Task<IIIFPrecursor> CreateIIIFPrecursor(string id, IIIF.Presentation.Version iiifVersion)
        {
            Response.ContentType = iiifVersion == IIIF.Presentation.Version.V2
                ? IIIFPresentation.ContentTypes.V2
                : IIIFPresentation.ContentTypes.V3;
            
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
                Pdf = string.Format(dlcsOptions.SkeletonNamedPdfTemplate, dlcsOptions.CustomerDefaultSpace, id),
                IIIFVersion = iiifVersion.ToString()
            };
            
            return model;
        }

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