using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DlcsWebClient.Config;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private IMetsRepository metsRepository;
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;
        private readonly DdsOptions ddsOptions;
        private readonly IAmazonS3 amazonS3;
        
        public IIIFBuilder(
            IMetsRepository metsRepository,
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DlcsOptions> dlcsOptions,
            IOptions<DdsOptions> ddsOptions,
            IAmazonS3 amazonS3)
        {
            this.metsRepository = metsRepository;
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.dlcsOptions = dlcsOptions.Value;
            this.ddsOptions = ddsOptions.Value;
            this.amazonS3 = amazonS3;
        }

        public async Task<BuildResult> BuildAllManifestations(string bNumber)
        {
            var result = new BuildResult();
            var ddsId = new DdsIdentifier(bNumber);
            if (ddsId.IdentifierType != IdentifierType.BNumber)
            {
                // we could throw an exception - do we actually care?
                // just process it. 
            }
            bNumber = ddsId.BNumber;
            var manifestationId = "start";
            try
            {
                // This will need some special tweaking to build Chemist and Druggist in the
                // Collection structure required for date-based navigation.
                var work = await catalogue.GetWork(ddsId.BNumber);
                await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(bNumber))
                {
                    var manifestation = manifestationInContext.Manifestation;
                    manifestationId = manifestation.Id;
                    var digitisedManifestation = await dashboardRepository.GetDigitisedResource(manifestationId);
                    result = await BuildInternal(work, digitisedManifestation);
                }
            }
            catch (Exception e)
            {
                result.Message = $"Failed at {manifestationId}, {e.Message}";
                result.Outcome = BuildOutcome.Failure;
            }

            return result;
        }


        private async Task<BuildResult> BuildInternal(Work work, IDigitisedResource digitisedResource)
        {
            var result = new BuildResult();
            try
            {
                CleanManifestation(digitisedResource);
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(digitisedResource, work);
                await SaveToS3(iiifPresentation3Resource, $"v3/{digitisedResource.Identifier}");
                // now build the Presentation 2 version from the Presentation 3 version
                var iiifPresentation2Resource = MakePresentation2Resource(iiifPresentation3Resource);
                await SaveToS3(iiifPresentation2Resource, $"v2/{digitisedResource.Identifier}");
                result.Outcome = BuildOutcome.Success;
            }
            catch (Exception e)
            {
                result.Message = e.Message;
                result.Outcome = BuildOutcome.Failure;
            }
            return result;
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
                result = await BuildInternal(work, digitisedResource);
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