using System.Collections.Generic;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
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
            var ddsTask = dashboardRepository.GetDigitisedResourceAsync(id);
            await Task.WhenAll(new List<Task> {ddsTask, workTask});

            var digitisedResource = ddsTask.Result;
            var work = workTask.Result;
            CleanManifestation(digitisedResource);
            var model = new IIIFPrecursor
            {
                Id = id,
                CatalogueMetadata = work,
                Label = digitisedResource.BNumberModel.DisplayTitle,
                Comment = "This is a " + digitisedResource.GetType(),
                ManifestSource = digitisedResource as DigitisedManifestation,
                SimpleCollectionSource = SimpleCollectionModel.MakeSimpleCollectionModel(digitisedResource as IDigitisedCollection),
                Pdf = string.Format(dlcsOptions.SkeletonNamedPdfTemplate, dlcsOptions.CustomerDefaultSpace, id)
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