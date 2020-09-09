using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using IIIF.Presentation;
using IIIF.Presentation.Constants;
using IIIF.Presentation.Content;
using IIIF.Presentation.Strings;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private readonly IDds dds;
        private readonly IMetsRepository metsRepository;
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DdsOptions ddsOptions;
        private readonly UriPatterns uriPatterns;
        private readonly IAmazonS3 amazonS3;
        
        public IIIFBuilder(
            IDds dds,
            IMetsRepository metsRepository,
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DdsOptions> ddsOptions,
            UriPatterns uriPatterns,
            IAmazonS3 amazonS3)
        {
            this.dds = dds;
            this.metsRepository = metsRepository;
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.ddsOptions = ddsOptions.Value;
            this.uriPatterns = uriPatterns;
            this.amazonS3 = amazonS3;
        }

        public async Task<BuildResult> BuildAndSaveAllManifestations(string bNumber, Work work = null)
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
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.BNumber);
                var resource = await dashboardRepository.GetDigitisedResource(bNumber);
                // This is a bnumber, so can't be part of anything.
                result = BuildInternal(work, resource, null, manifestationMetadata);
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
                        result = BuildInternal(work, digitisedManifestation, parentCollection, manifestationMetadata);
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



        private BuildResult BuildInternal(
            Work work, 
            IDigitisedResource digitisedResource, IDigitisedCollection partOf,
            ManifestationMetadata manifestationMetadata)
        {
            var result = new BuildResult();
            try
            {
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(digitisedResource, partOf, work, manifestationMetadata);
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
        
        public async Task<BuildResult> Build(string identifier, Work work = null)
        {
            // only this identifier, not all for the b number.
            var result = new BuildResult();
            try
            {
                var ddsId = new DdsIdentifier(identifier);
                var digitisedResource = await dashboardRepository.GetDigitisedResource(identifier);
                work ??= await catalogue.GetWorkByOtherIdentifier(ddsId.BNumber);
                var manifestationMetadata = dds.GetManifestationMetadata(ddsId.BNumber);
                IDigitisedCollection partOf = null;
                if (ddsId.IdentifierType != IdentifierType.BNumber)
                {
                    // this identifier has a parent, which we will need to build the resource properly
                    // this parent property smells... need to do some work to make sure this is always an identical
                    // result to BuildAndSaveAllManifestations
                    partOf = await dashboardRepository.GetDigitisedResource(ddsId.Parent) as IDigitisedCollection;
                }
                result = BuildInternal(work, digitisedResource, partOf, manifestationMetadata);
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
        /// <param name="manifestationMetadata"></param>
        /// <returns></returns>
        public StructureBase MakePresentation3Resource(
            IDigitisedResource digitisedResource,
            IDigitisedCollection partOf,
            Work work,
            ManifestationMetadata manifestationMetadata)
        {
            switch (digitisedResource)
            {
                case IDigitisedCollection digitisedCollection:
                    var collection = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(digitisedCollection.Identifier)
                    };
                    AddCommonMetadata(collection, work, manifestationMetadata);
                    BuildCollection(collection, digitisedCollection, work);
                    return collection;
                case IDigitisedManifestation digitisedManifestation:
                    var manifest = new Manifest
                    {
                        Id = uriPatterns.Manifest(digitisedManifestation.Identifier)
                    };
                    if (partOf != null)
                    {
                        manifest.PartOf = new List<ResourceBase>
                        {
                            new Collection
                            {
                                Id = uriPatterns.CollectionForWork(partOf.Identifier),
                                Label = new LanguageMap("en", work.Title)
                            }
                        };
                    }
                    AddCommonMetadata(manifest, work, manifestationMetadata);
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
        /// <param name="manifestationMetadata"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void AddCommonMetadata(
            StructureBase iiifResource, Work work,
            ManifestationMetadata manifestationMetadata)
        {
            iiifResource.AddPresentation3Context();
            iiifResource.Label = Lang.Map(work.Title);
            AddSeeAlso(iiifResource, work);
            AddWellcomeProvider(iiifResource);
            AddOtherProvider(iiifResource, manifestationMetadata);
            AddAggregations(iiifResource, manifestationMetadata);
        }

        private void AddAggregations(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var groups = manifestationMetadata.Metadata.GroupBy(m => m.Label);
            foreach (var @group in groups)
            {
                foreach (var md in @group)
                {
                    iiifResource.PartOf ??= new List<ResourceBase>();
                    iiifResource.PartOf.Add(
                        new Collection
                        {
                            Id = uriPatterns.CollectionForAggregation(md.Label, md.StringValue),
                            Label = new LanguageMap("en", $"{md.Label}: {md.StringValue}")
                        });
                }
            }
        }

        private void AddOtherProvider(ResourceBase iiifResource, ManifestationMetadata manifestationMetadata)
        {
            var locationOfOriginal = manifestationMetadata.Metadata
                .FirstOrDefault(m => m.Label == "Location");
            if (locationOfOriginal == null) return;
            var agent = PartnerAgents.GetAgent(locationOfOriginal.StringValue, ddsOptions.LinkedDataDomain);
            if (agent == null) return;
            iiifResource.Provider ??= new List<Agent>();
            iiifResource.Provider.Add(agent);
        }

        private void AddWellcomeProvider(ResourceBase iiifResource)
        {
            var agent = new Agent
            {
                Id = "https://wellcomecollection.org",
                Label = Lang.Map("en",
                    "Wellcome Collection",
                    "183 Euston Road",
                    "London NW1 2BE",
                    "UK"),
                HomePage = new List<ExternalResource>
                {
                    new ExternalResource("Text")
                    {
                        Id = "https://wellcomecollection.org/works",
                        Label = Lang.Map("Explore our collections"),
                        Format = "text/html"
                    }
                },
                Logo = new List<Image>
                {
                    // TODO - Wellcome Collection logo
                    new Image
                    {
                        Id = "https://wellcomelibrary.org/assets/img/squarelogo64.png",
                        Format = "image/png"
                    }
                }
            };
            iiifResource.Provider = new List<Agent>{agent};
        }

        private void AddSeeAlso(ResourceBase iiifResource, Work work)
        {
            iiifResource.SeeAlso = new List<ExternalResource>
            {
                new ExternalResource("Dataset")
                {
                    Id = uriPatterns.CatalogueApi(work.Id, new string[]{}),
                    Label = Lang.Map("Wellcome Collection Catalogue API"),
                    Format = "application/json",
                    Profile = "https://api.wellcomecollection.org/catalogue/v2/context.json"
                }
            };
        }


        private void BuildManifest(Manifest manifest, IDigitisedManifestation digitisedManifestation)
        {
            // throw new NotImplementedException();
        }

        private void BuildCollection(Collection collection, IDigitisedCollection digitisedCollection, Work work)
        {
            // TODO - use of Labels.
            // The work label should be preferred over the METS label,
            // but sometimes there is structural (volume) labelling that the catalogue API won't know about.
            collection.Items = new List<ICollectionItem>();
            if (digitisedCollection.Collections.HasItems())
            {
                foreach (var coll in digitisedCollection.Collections)
                {
                    collection.Items.Add(new Collection
                    {
                        Id = uriPatterns.CollectionForWork(coll.Identifier),
                        Label = Lang.Map(coll.MetsCollection.Label)
                    });
                }
            }

            if (digitisedCollection.Manifestations.HasItems())
            {
                int counter = 1;
                foreach (var mf in digitisedCollection.Manifestations)
                {
                    var order = mf.MetsManifestation.Order;
                    if (!order.HasValue || order < 1)
                    {
                        order = counter;
                    }
                    collection.Items.Add(new Manifest
                    {
                        Id = uriPatterns.Manifest(mf.Identifier),
                        Label = new LanguageMap
                        {
                            ["en"] = new List<string>
                            {
                                $"Volume {order}",
                                work.Title
                            }
                        }
                        // LangMap($"{mf.MetsManifestation.Label} - {order}")
                    });
                    counter++;
                }
            }
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
                Label = Lang.Map("[IIIF 2.1 version of] " + iiifPresentation3Resource.Label)
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
            await amazonS3.PutObjectAsync(put);
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

    }
}