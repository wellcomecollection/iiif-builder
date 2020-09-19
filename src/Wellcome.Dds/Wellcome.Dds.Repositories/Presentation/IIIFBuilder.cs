﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using IIIF;
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
using Wellcome.Dds.Repositories.Presentation.SpecialState;

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
        private readonly IIIFBuilderParts build;
        
        public IIIFBuilder(
            IDds dds,
            IMetsRepository metsRepository,
            IDashboardRepository dashboardRepository,
            ICatalogue catalogue,
            IOptions<DdsOptions> ddsOptions,
            IOptions<DlcsOptions> dlcsOptions,
            UriPatterns uriPatterns)
        {
            this.dds = dds;
            this.metsRepository = metsRepository;
            this.dashboardRepository = dashboardRepository;
            this.catalogue = catalogue;
            this.ddsOptions = ddsOptions.Value;
            this.uriPatterns = uriPatterns;
            build = new IIIFBuilderParts(uriPatterns, dlcsOptions.Value.CustomerDefaultSpace);
        }

        public async Task<MultipleBuildResult> BuildAllManifestations(string bNumber, Work work = null)
        {
            var state = new State();
            var buildResults = new MultipleBuildResult();
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
                buildResults.Add(BuildInternal(work, resource, null, manifestationMetadata, state));
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
                        buildResults.Add(BuildInternal(work, digitisedManifestation, parentCollection, manifestationMetadata, state));
                    }
                }
            }
            catch (Exception e)
            {
                buildResults.Message = $"Failed at {manifestationId}, {e.Message}";
                buildResults.Outcome = BuildOutcome.Failure;
                return buildResults;
            }

            CheckAndProcessState(buildResults, state);
            
            // Now the buildResults should have what actually needs to be persisted.
            // Only now do we convert them to IIIF2
            foreach (var buildResult in buildResults)
            {
                // now build the Presentation 2 version from the Presentation 3 version
                var iiifPresentation2Resource = MakePresentation2Resource(buildResult.IIIF3Resource);
                buildResult.IIIF2Resource = iiifPresentation2Resource;
                buildResult.IIIF2Key = $"v2/{buildResult.Id}";
            }
            
            return buildResults;
        }

        private void CheckAndProcessState(MultipleBuildResult buildResults, State state)
        {
            if (!state.HasState)
            {
                return;
            }

            if (state.MultiCopyState != null)
            {
                // We should have ended up with a Collection, comprising more than one Manifest.
                // Each Manifest will have a copy number.
                // There might be more than one volume per copy, in which case we have
                // a nested collection.
                if (!(buildResults.First().IIIF3Resource is Collection bNumberCollection))
                {
                    throw new IIIFBuildStateException("State is missing the parent collection");
                }

                var newItems = bNumberCollection.Items = new List<ICollectionItem>();
                var copies = state.MultiCopyState.CopyAndVolumes.Values
                    .Select(cv => cv.CopyNumber)
                    .Distinct().ToList(); // leave in the order we find them
                foreach (int copy in copies)
                {
                    var volumesForCopy = state.MultiCopyState.CopyAndVolumes.Values
                        .Where(cv => cv.CopyNumber == copy)
                        .Select(cv => cv.VolumeNumber)
                        .ToList();
                    if (volumesForCopy.Count > 1)
                    {
                        // This volume is a collection child of the root;
                        // create the copy collection then add its volumes
                        var copyCollection = new Collection
                        {
                            Id = $"{bNumberCollection.Id}-copy-{copy}",
                            Label = Lang.Map($"Copy {copy}"),
                            Items = new List<ICollectionItem>()
                        };
                        newItems.Add(copyCollection);
                        foreach (int volume in volumesForCopy)
                        {
                            var identifiersForVolume =
                                state.MultiCopyState.CopyAndVolumes.Values
                                    .Where(cv => cv.CopyNumber == copy && cv.VolumeNumber == volume);
                            foreach (var copyAndVolume in identifiersForVolume)
                            {
                                var manifestResult = buildResults.Single(br => br.Id == copyAndVolume.Id);
                                var manifest = (Manifest) manifestResult.IIIF3Resource;
                                manifest.Label?.Values.First().Add($"Copy {copy}, Volume {volume}");
                                copyCollection.Items.Add(new Manifest
                                {
                                    Id = manifest.Id,
                                    Label = manifest.Label,
                                    Thumbnail = manifest.Thumbnail
                                });
                                if (manifest.Thumbnail.HasItems())
                                {
                                    copyCollection.Thumbnail ??= new List<ExternalResource>();
                                    copyCollection.Thumbnail.AddRange(manifest.Thumbnail);
                                }
                            }
                        }
                    }
                    else
                    {
                        // This copy is attached to the b-number collection directly
                        // accept multiple copies with the same number, just in case
                        var identifiersForCopy =
                            state.MultiCopyState.CopyAndVolumes.Values.Where(cv => cv.CopyNumber == copy);
                        foreach (var copyAndVolume in identifiersForCopy)
                        {
                            var manifestResult = buildResults.Single(br => br.Id == copyAndVolume.Id);
                            var manifest = (Manifest) manifestResult.IIIF3Resource;
                            manifest.Label?.Values.First().Add($"Copy {copy}");
                            newItems.Add(new Manifest
                            {
                                Id = manifest.Id,
                                Label = manifest.Label,
                                Thumbnail = manifest.Thumbnail
                            });
                        }
                    }
                }
            } 
            else if (state.AVState != null)
            {
                
            }
            else if (state.ChemistAndDruggistState != null)
            {
                // Fear me...
            }
        }


        public async Task<MultipleBuildResult> Build(string identifier, Work work = null)
        {
            // only this identifier, not all for the b number.
            var buildResults = new MultipleBuildResult();
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
                // We can't supply state for a single build.
                // We should throw an exception. 
                // The dash preview can choose to handle this, for multicopy and AV, but not for C&D.
                // (dash preview can go back and rebuild all, then pick out the one asked for, 
                // or redirect to a single AV manifest).
                buildResults.Add(BuildInternal(work, digitisedResource, partOf, manifestationMetadata, null));
            }
            catch (Exception e)
            {
                buildResults.Message = e.Message;
                buildResults.Outcome = BuildOutcome.Failure;
            }
            return buildResults;
        }
        

        private BuildResult BuildInternal(Work work,
            IDigitisedResource digitisedResource, IDigitisedCollection partOf,
            ManifestationMetadata manifestationMetadata, State state)
        {
            var result = new BuildResult(digitisedResource.Identifier);
            try
            {
                // build the Presentation 3 version from the source materials
                var iiifPresentation3Resource = MakePresentation3Resource(
                    digitisedResource, partOf, work, manifestationMetadata, state);
                result.IIIF3Resource = iiifPresentation3Resource;
                result.IIIF3Key = $"v3/{digitisedResource.Identifier}";
                result.Outcome = BuildOutcome.Success;
            }
            catch (IIIFBuildStateException bex)
            {
                result.RequiresMultipleBuild = true;
                result.Message = bex.Message;
                result.Outcome = BuildOutcome.Failure;
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
        private StructureBase MakePresentation3Resource(
            IDigitisedResource digitisedResource,
            IDigitisedCollection partOf,
            Work work,
            ManifestationMetadata manifestationMetadata,
            State state)
        {
            switch (digitisedResource)
            {
                case IDigitisedCollection digitisedCollection:
                    var collection = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(digitisedCollection.Identifier)
                    };
                    AddCommonMetadata(collection, work, manifestationMetadata);
                    BuildCollection(collection, digitisedCollection, work, manifestationMetadata);
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
                                Label = Lang.Map(work.Title),
                                Behavior = new List<string>{Behavior.MultiPart}
                            }
                        };
                    }
                    AddCommonMetadata(manifest, work, manifestationMetadata);
                    BuildManifest(manifest, digitisedManifestation, manifestationMetadata, state);
                    return manifest;
            }
            throw new NotSupportedException("Unhandled type of Digitised Resource");
        }

        private void BuildCollection(
            Collection collection,
            IDigitisedCollection digitisedCollection,
            Work work,
            ManifestationMetadata manifestationMetadata)
        {
            // TODO - use of Labels.
            // The work label should be preferred over the METS label,
            // but sometimes there is structural (volume) labelling that the catalogue API won't know about.
            collection.Items = new List<ICollectionItem>();
            collection.Behavior ??= new List<string>();
            collection.Behavior.Add(Behavior.MultiPart);
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
                        },
                        Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(mf.Identifier)
                    });
                    counter++;
                }
            }
        }
        



        private void BuildManifest(
            Manifest manifest, 
            IDigitisedManifestation digitisedManifestation,
            ManifestationMetadata manifestationMetadata,
            State state)
        {
            manifest.Thumbnail = manifestationMetadata.Manifestations.GetThumbnail(digitisedManifestation.Identifier);
            build.RequiredStatement(manifest, digitisedManifestation, manifestationMetadata);
            build.Rights(manifest, digitisedManifestation);
            build.PagedBehavior(manifest, digitisedManifestation);
            build.ViewingDirection(manifest, digitisedManifestation); // do we do this?
            build.Rendering(manifest, digitisedManifestation);
            build.SearchServices(manifest, digitisedManifestation);
            build.Canvases(manifest, digitisedManifestation);
            // do this next... both the next two use the manifestStructureHelper
            build.Structures(manifest, digitisedManifestation); // ranges
            build.ImprovePagingSequence(manifest);
            build.CheckForCopyAndVolumeStructure(digitisedManifestation, state);
            build.ManifestLevelAnnotations(manifest, digitisedManifestation);
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
            build.SeeAlso(iiifResource, work);
            iiifResource.AddWellcomeProvider(ddsOptions.LinkedDataDomain);
            iiifResource.AddOtherProvider(manifestationMetadata, ddsOptions.LinkedDataDomain);
            build.Aggregations(iiifResource, manifestationMetadata);
            build.Summary(iiifResource, work);
            build.HomePage(iiifResource, work);
            build.Metadata(iiifResource, work);
            build.ArchiveCollectionStructure(iiifResource, work);
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
                ContractResolver = new PrettyIIIFContractResolver(),
                Formatting = Formatting.Indented
            };
            return settings;
        }

    }
}