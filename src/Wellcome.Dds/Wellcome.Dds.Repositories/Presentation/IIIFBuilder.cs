using System.Collections.Generic;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIF;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;
        
        public IIIFBuilder(
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DlcsOptions> dlcsOptions)
        {
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.dlcsOptions = dlcsOptions.Value;
        }
        
        public async Task<BuildResult> Build(string identifier)
        {
            var ddsId = new DdsIdentifier(identifier);
            var workTask = catalogue.GetWork(ddsId.BNumber);
            var ddsTask = dashboardRepository.GetDigitisedResource(identifier);
            await Task.WhenAll(new List<Task> {ddsTask, workTask});
            
            
            var digitisedResource = ddsTask.Result;
            var work = workTask.Result;
            CleanManifestation(digitisedResource);

            // build the Presentation 3 version from the source materials
            var iiifPresentation3Resource = MakePresentation3Resource(digitisedResource, work);
            SaveToS3(iiifPresentation3Resource, $"v3/{digitisedResource.Identifier}");
            // now build the Presentation 2 version from the Presentation 3 version
            var iiifPresentation2Resource = MakePresentation2Resource(iiifPresentation3Resource);
            SaveToS3(iiifPresentation2Resource, $"v2/{digitisedResource.Identifier}");

            return new BuildResult
            {
                Outcome = BuildOutcome.Success
            };
        }


        /// <summary>
        /// This obviously WILL NOT be a IIIFPrecursor
        /// </summary>
        /// <param name="digitisedResource"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        private IIIFPrecursor MakePresentation3Resource(IDigitisedResource digitisedResource, Work work)
        {            
            var iiif = new IIIFPrecursor
            {
                Id = digitisedResource.Identifier,
                CatalogueMetadata = work,
                Label = digitisedResource.BNumberModel.DisplayTitle,
                Comment = $"This is a {digitisedResource.GetType()}",
                ManifestSource = digitisedResource as DigitisedManifestation,
                SimpleCollectionSource = SimpleCollectionModel.MakeSimpleCollectionModel(digitisedResource as IDigitisedCollection),
                Pdf = string.Format(dlcsOptions.SkeletonNamedPdfTemplate, dlcsOptions.CustomerDefaultSpace, digitisedResource.Identifier),
                IIIFVersion = "3"
            };
            return iiif;
        }
        
        
        /// <summary>
        /// This obviously WILL NOT be a IIIFPrecursor
        /// </summary>
        /// <param name="digitisedResource"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        private IIIFPrecursor MakePresentation2Resource(IIIFPrecursor iiifPresentation3Resource)
        {
            // This just changes the version and returns the same object. OK because we've
            // already written the v3 one to S3. But obviously, don't do it like this for real!
            iiifPresentation3Resource.IIIFVersion = "2";
            return iiifPresentation3Resource;
        }

        
        private void SaveToS3(IIIFPrecursor iiifResource, string key)
        {
            throw new System.NotImplementedException();
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