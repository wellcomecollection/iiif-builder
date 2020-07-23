using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
        private IDashboardRepository dashboardRepository;
        private ICatalogue catalogue;
        
        public PresentationController(
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue)
        {
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
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
                SimpleCollectionSource = SimpleCollectionModel.MakeSimpleCollectionModel(digitisedResource as IDigitisedCollection)
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