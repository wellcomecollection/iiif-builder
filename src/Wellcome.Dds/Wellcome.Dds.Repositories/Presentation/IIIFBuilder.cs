using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DlcsWebClient.Config;
using IIIF.Presentation;
using IIIF.Presentation.Strings;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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
        private readonly IMetsRepository metsRepository;
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DdsOptions ddsOptions;
        private readonly UriPatterns uriPatterns;
        private readonly IAmazonS3 amazonS3;
        
        public IIIFBuilder(
            IMetsRepository metsRepository,
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DdsOptions> ddsOptions,
            UriPatterns uriPatterns,
            IAmazonS3 amazonS3)
        {
            this.metsRepository = metsRepository;
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.ddsOptions = ddsOptions.Value;
            this.uriPatterns = uriPatterns;
            this.amazonS3 = amazonS3;
        }

        public async Task<BuildResult> BuildAndSaveAllManifestations(string bNumber)
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
                var work = await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var resource = await dashboardRepository.GetDigitisedResource(bNumber);
                // This is a bnumber, so can't be part of anything.
                result = BuildInternal(work, resource, null);
                await Save(result);
                if (resource is IDigitisedCollection parentCollection)
                {
                    // This will need some special treatment to build Chemist and Druggist in the
                    // Collection structure required for date-based navigation, and handle the two levels.
                    // Come back to that once we have the basics working.
                    // C&D needs a special build process.
                    await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(bNumber))
                    {
                        var manifestation = manifestationInContext.Manifestation;
                        manifestationId = manifestation.Id;
                        var digitisedManifestation = await dashboardRepository.GetDigitisedResource(manifestationId);
                        result = BuildInternal(work, digitisedManifestation, parentCollection);
                        await Save(result);
                    }
                }
            }
            catch (Exception e)
            {
                result.Message = $"Failed at {manifestationId}, {e.Message}";
                result.Outcome = BuildOutcome.Failure;
            }

            return result;
        }


        private BuildResult BuildInternal(Work work, IDigitisedResource digitisedResource, IDigitisedCollection partOf)
        {
            var result = new BuildResult();
            try
            {
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(digitisedResource, partOf, work);
                result.IIIF3Resource = iiifPresentation3Resource;
                result.IIIF3Key = $"v3/{digitisedResource.Identifier}";
                
                // now build the Presentation 2 version from the Presentation 3 version
                var iiifPresentation2Resource = MakePresentation2Resource(iiifPresentation3Resource);
                result.IIIF2Resource = iiifPresentation2Resource;
                result.IIIF2Key = $"v2/{digitisedResource.Identifier}";
                
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
            // only this identifier, not all for the b number.
            var result = new BuildResult();
            try
            {
                var ddsId = new DdsIdentifier(identifier);
                var workTask = catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var ddsTask = dashboardRepository.GetDigitisedResource(identifier);
                await Task.WhenAll(new List<Task> {ddsTask, workTask});
                var digitisedResource = ddsTask.Result;
                var work = workTask.Result;
                IDigitisedCollection partOf = null;
                if (ddsId.IdentifierType != IdentifierType.BNumber)
                {
                    // this identifier has a parent, which we will need to build the resource properly
                    // this parent property smells... need to do some work to make sure this is always an identical
                    // result to BuildAndSaveAllManifestations
                    partOf = await dashboardRepository.GetDigitisedResource(ddsId.Parent) as IDigitisedCollection;
                }
                result = BuildInternal(work, digitisedResource, partOf);
            }
            catch (Exception e)
            {
                result.Message = e.Message;
                result.Outcome = BuildOutcome.Failure;
            }
            return result;
        }


        /// <summary>
        /// </summary>
        /// <param name="digitisedResource"></param>
        /// <param name="partOf"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        public StructureBase MakePresentation3Resource(
            IDigitisedResource digitisedResource,
            IDigitisedCollection partOf,
            Work work)
        {
            switch (digitisedResource)
            {
                case IDigitisedCollection digitisedCollection:
                    var collection = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(digitisedCollection.Identifier)
                    };
                    AddCommonMetadata(collection, work);
                    BuildCollection(collection, digitisedCollection);
                    return collection;
                case IDigitisedManifestation digitisedManifestation:
                    var manifest = new Manifest
                    {
                        Id = uriPatterns.Manifest(digitisedManifestation.Identifier)
                    };
                    manifest.PartOf = new List<ResourceBase>
                    {
                        new Collection
                        {
                            Id = uriPatterns.CollectionForWork(partOf.Identifier),
                            Label = new LanguageMap("en", work.Title)
                        }
                    };
                    AddCommonMetadata(manifest, work);
                    BuildManifest(manifest, digitisedManifestation);
                    return manifest;
            }
            throw new NotSupportedException("Unhandled type of Digitised Resource");
        }

        /// <summary>
        /// Metadata, providers etc that are found on Work-level collections as
        /// well as manifests
        /// </summary>
        /// <param name="iiifResource"></param>
        /// <param name="work"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void AddCommonMetadata(StructureBase iiifResource, Work work)
        {
            iiifResource.Label = LangMap(work.Title);
        }

        
        private void BuildManifest(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            throw new NotImplementedException();
        }

        private void BuildCollection(Collection collection, IDigitisedCollection digitisedCollection)
        {
            throw new NotImplementedException();    
        }


        /// <summary>
        /// Convert the IIIF v3 Manifest into its equivalent v2 manifest
        /// </summary>
        /// <returns></returns>
        private StructureBase MakePresentation2Resource(StructureBase iiifPresentation3Resource)
        {
            // TODO - this is obviously a placeholder!
            var p2Version = new Manifest
            {
                Label = LangMap("[IIIF 2.1 version of] " + iiifPresentation3Resource.Label)
            };
            return p2Version;
        }

        private async Task Save(BuildResult buildResult)
        {
            await SaveToS3(buildResult.IIIF3Resource, buildResult.IIIF3Key);
            await SaveToS3(buildResult.IIIF2Resource, buildResult.IIIF2Key);
        }
        
        private async Task SaveToS3(StructureBase iiifResource, string key)
        {
            var put = new PutObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key,
                ContentBody = Serialise(iiifResource),
                ContentType = "application/json"
            };
            var resp = await amazonS3.PutObjectAsync(put);
        }

        public string Serialise(StructureBase iiifResource)
        {
            return JsonConvert.SerializeObject(iiifResource, GetJsFriendlySettings());
        }
        // we'll need another Serialise for IIIFv2

        private static JsonSerializerSettings GetJsFriendlySettings()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new IgnoreEmptyListResolver(),
                Formatting = Formatting.Indented
            };
            return settings;
        }

        private LanguageMap LangMap(string value)
        {
            return new LanguageMap("en", value);
        }
        
        private LanguageMap LangMap(string lang, string value)
        {
            return new LanguageMap(lang, value);
        }
    }
}