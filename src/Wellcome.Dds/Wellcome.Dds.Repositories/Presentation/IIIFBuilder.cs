using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DlcsWebClient.Config;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;
        private readonly DdsOptions ddsOptions;
        private readonly IAmazonS3 amazonS3;
        
        public IIIFBuilder(
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DlcsOptions> dlcsOptions,
            IOptions<DdsOptions> ddsOptions,
            IAmazonS3 amazonS3)
        {
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.dlcsOptions = dlcsOptions.Value;
            this.ddsOptions = ddsOptions.Value;
            this.amazonS3 = amazonS3;
        }
        
        public async Task<BuildResult> Build(string identifier)
        {
            var result = new BuildResult();
            try
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
                await SaveToS3(iiifPresentation3Resource, $"v3/{digitisedResource.Identifier}");
                // now build the Presentation 2 version from the Presentation 3 version
                var iiifPresentation2Resource = MakePresentation2Resource(iiifPresentation3Resource);
                await SaveToS3(iiifPresentation2Resource, $"v2/{digitisedResource.Identifier}");

                
                // TODO - identifier comes in as a b number; you have to make ALL of them!
                // (walk down the collection creating IIIF resources
                // If this is a collection, atm it will just make the collection and not its parts.
                
                result.Outcome = BuildOutcome.Success;
            }
            catch (Exception e)
            {
                result.Message = e.Message;
                result.Outcome = BuildOutcome.Failure;
            }

            return result;
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
                IIIFVersion = IIIF.Presentation.Version.V3.ToString()
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
            iiifPresentation3Resource.IIIFVersion = IIIF.Presentation.Version.V2.ToString();
            return iiifPresentation3Resource;
        }

        
        private async Task SaveToS3(IIIFPrecursor iiifResource, string key)
        {
            var json = JsonConvert.SerializeObject(iiifResource);
            var put = new PutObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key,
                ContentBody = json,
                ContentType = "application/json"
            };
            var resp = await amazonS3.PutObjectAsync(put);
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