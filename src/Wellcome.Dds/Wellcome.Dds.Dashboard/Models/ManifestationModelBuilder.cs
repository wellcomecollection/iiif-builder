using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using DlcsWebClient.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Logging;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Controllers;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Dashboard.Models
{
    public class ManifestationModelBuilder
    {
        private readonly IAmazonS3 amazonS3;
        private readonly ILogger<ManifestationModelBuilder> logger;
        private readonly ICatalogue catalogue;
        private readonly IDigitalObjectRepository digitalObjectRepository;
        private readonly ISimpleCache cache;
        private readonly UriPatterns uriPatterns;
        private readonly DdsOptions ddsOptions;
        private readonly DlcsOptions dlcsOptions;
        private readonly SmallJobLogger jobLogger;
        private readonly IMetsRepository metsRepository;
        private readonly IIdentityService identityService;

        private const string CacheKeyPrefix = "manifestModelBuilder_";
        private const int CacheSeconds = 5;

        public ManifestationModelBuilder(
            IOptions<DdsOptions> ddsOptions,
            IOptions<DlcsOptions> dlcsOptions,
            IAmazonS3 amazonS3,
            ILogger<ManifestationModelBuilder> logger,
            ICatalogue catalogue,
            IDigitalObjectRepository digitalObjectRepository,
            ISimpleCache cache,
            UriPatterns uriPatterns,
            IMetsRepository metsRepository,
            IIdentityService identityService
        )
        {
            this.ddsOptions = ddsOptions.Value;
            this.amazonS3 = amazonS3;
            this.logger = logger;
            this.catalogue = catalogue;
            this.digitalObjectRepository = digitalObjectRepository;
            this.cache = cache;
            this.uriPatterns = uriPatterns;
            this.dlcsOptions = dlcsOptions.Value;
            this.metsRepository = metsRepository;
            this.identityService = identityService;
            jobLogger = new SmallJobLogger(string.Empty, null);
        }

        public async Task<OverallResult> Build(DdsIdentity identifier, IUrlHelper url)
        {
            try
            {
                jobLogger.Start();
                jobLogger.Log(
                    "Start parallel dashboardRepository.GetDigitisedResource(id), catalogue.GetWorkByOtherIdentifier(ddsId.BNumber)");
                var workTask = catalogue.GetWorkByOtherIdentifier(identifier.PackageIdentifier);
                var dlcsCallContext = new DlcsCallContext("ManifestationModelBuilder::Build", identifier.Value);
                logger.LogDebug("Starting DlcsCallContext {callContext}", dlcsCallContext);
                var includePdfDetails = identifier.Generator == Generator.Goobi;
                var ddsTask = digitalObjectRepository.GetDigitalObject(identifier.Value, dlcsCallContext, includePdfDetails);
                await Task.WhenAll(new List<Task> {ddsTask, workTask});
                var dgResource = ddsTask.Result;
                var work = workTask.Result;

                if (dgResource is IDigitalManifestation dgManifestation)
                {
                    // ***************************************************
                    // THIS IS ONLY HERE TO SUPPORT THE PDF LINK
                    // IT MUST GO AS SOON AS THE IIIF MANIFEST KNOWS String3
                    jobLogger.Log("Start dashboardRepository.FindSequenceIndex(id)");
                    // dgManifestation.SequenceIndex = await dashboardRepository.FindSequenceIndex(id);
                    jobLogger.Log("Finished dashboardRepository.FindSequenceIndex(id)");
                    // represents the set of differences between the METS view of the world and the DLCS view
                    jobLogger.Log("Start dashboardRepository.GetDlcsSyncOperation(id)");
                    var syncOperation = await digitalObjectRepository.GetDlcsSyncOperation(dgManifestation, true, dlcsCallContext);
                    jobLogger.Log("Finished dashboardRepository.GetDlcsSyncOperation(id)");

                    ICollection parent;
                    ICollection grandparent;
                    // We need to show the manifestation with information about its parents if it has any.
                    // This allows navigation through multiple manifestations.
                    if (identifier.IssuePart.HasText())
                    {
                        parent = await GetCachedCollectionAsync(identifier.VolumePart);
                        grandparent = await GetCachedCollectionAsync(identifier.PackageIdentifier);
                    }
                    else if (identifier.VolumePart.HasText())
                    {
                        parent = await GetCachedCollectionAsync(identifier.PackageIdentifier);
                        grandparent = null;
                    }
                    else
                    {
                        parent = null;
                        grandparent = null;
                    }

                    var skeletonPreview = string.Format(
                        dlcsOptions.SkeletonNamedQueryTemplate!, dlcsOptions.CustomerDefaultSpace, identifier);

                    var model = new ManifestationModel
                    {
                        DefaultSpace = digitalObjectRepository.DefaultSpace,
                        Url = url,
                        DdsIdentifier = identifier,
                        DigitisedManifestation = dgManifestation,
                        Parent = parent,
                        GrandParent = grandparent,
                        SyncOperation = syncOperation,
                        DlcsOptions = dlcsOptions,
                        DlcsSkeletonManifest = skeletonPreview,
                        Work = work,
                        ManifestUrl = uriPatterns.Manifest(identifier.Value)
                    };
                    if (work != null)
                    {
                        // It's OK, in the dashboard, for a Manifestation to not have a corresponding work.
                        // We can't make IIIF for it, though.
                        model.CatalogueApi = uriPatterns.CatalogueApi(work.Id);
                        model.CatalogueApiFull = catalogue.GetCatalogueApiUrl(work.Id);
                        model.WorkPage = uriPatterns.PersistentPlayerUri(work.Id);
                    }

                    model.DeliveredFilesMap = new Dictionary<string, DeliveredFile[]>();
                    foreach (var file in dgManifestation.MetsManifestation!.SynchronisableFiles!)
                    {
                        model.DeliveredFilesMap[file.StorageIdentifier!] =
                            digitalObjectRepository.GetDeliveredFiles(file);
                    }
                    
                    model.MakeManifestationNavData(identityService);
                    jobLogger.Log("Start dashboardRepository.GetRationalisedJobActivity(syncOperation)");
                    var jobActivity = await digitalObjectRepository.GetRationalisedJobActivity(syncOperation, dlcsCallContext);
                    jobLogger.Log("Finished dashboardRepository.GetRationalisedJobActivity(syncOperation)");
                    model.IngestJobs = jobActivity.UpdatedJobs;
                    model.BatchesForImages = jobActivity.BatchesForCurrentImages;

                    model.DbJobIdsToActiveBatches = new Dictionary<int, List<Batch>>();
                    foreach (var dlcsIngestJob in model.IngestJobs)
                    {
                        if (!model.DbJobIdsToActiveBatches.ContainsKey(dlcsIngestJob.Id))
                        {
                            model.DbJobIdsToActiveBatches[dlcsIngestJob.Id] = new List<Batch>();
                        }

                        jobLogger.Log("Start enumerating job batches");
                        foreach (var dbBatch in dlcsIngestJob.DlcsBatches)
                        {
                            // Move this to the repository
                            if (dbBatch.ResponseBody.HasText())
                            {
                                Batch reportedDbBatch;
                                try
                                {
                                    reportedDbBatch = JsonConvert.DeserializeObject<Batch>(dbBatch.ResponseBody);
                                }
                                catch
                                {
                                    logger.LogWarning("Batch response in Instrumentation DB is not a JSON Batch: body is {responseBody}", dbBatch.ResponseBody);
                                    continue;
                                }
                                var activeBatch = model.BatchesForImages.SingleOrDefault(b => b.Id == reportedDbBatch.Id);
                                if (activeBatch == null) continue;
                                
                                if (!model.DbJobIdsToActiveBatches[dlcsIngestJob.Id].Exists(b => b.Id == activeBatch.Id))
                                {
                                    model.DbJobIdsToActiveBatches[dlcsIngestJob.Id].Add(activeBatch);
                                }
                            }
                        }

                        jobLogger.Log("Finished enumerating job batches");
                    }

                    model.IsRunning = syncOperation.DlcsImagesCurrentlyIngesting.Count > 0;
                    
                    jobLogger.Log("Start S3 file queries");
                    await PopulateLastWriteTimes(model);
                    jobLogger.Log("Finish S3 file queries");
                    
                    return new OverallResult
                    {
                        Model = model
                    };
                }

                if (dgResource is IDigitalCollection dgCollection)
                {
                    // This is the manifestation controller, not the volume or issue controller.
                    // So redirect to the first manifestation that we can find for this collection.
                    // Put this and any intermediary collections in the short term cache,
                    // so that we don't need to build them from scratch after the redirect.
                    string redirectId;
                    // PutCollectionInShortTermCache(dgCollection);
                    if (dgCollection.MetsCollection.Manifestations.HasItems())
                    {
                        // a normal multiple manifestation, or possibly a periodical volume?
                        redirectId = dgCollection.MetsCollection.Manifestations.First().Identifier;
                        return new OverallResult
                        {
                            RedirectToManifest = true,
                            RedirectId = redirectId
                        };
                    }
                    
                    // a periodical, I think - but go to volume controller for this.
                    redirectId = identityService.GetIdentity(dgCollection.MetsCollection.Identifier).PackageIdentifier;
                    return new OverallResult
                    {
                        RedirectToCollection = true,
                        RedirectId = redirectId
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting manifestation for '{id}'", identifier);
                throw;
            }
            finally
            {
                jobLogger.Stop();
            }

            return null;
        }

        public List<Tuple<long, long, string>> GetLoggingEvents() => jobLogger.GetEvents();
        
        // private void PutCollectionInShortTermCache(IDigitalCollection collection)
        // {
        //     // (in order to PUT this in the cache, we need to retrieve it...
        //     var key = $"{CacheKeyPrefix}digital_{collection.Identifier}";
        //     cache.Remove(key);
        //     
        //     // TODO - should this be .Insert() as we've removed cache? 
        //     cache.GetCached(CacheSeconds, key, () => collection); 
        // }
        
        private async Task<ICollection> GetCachedCollectionAsync(string identifier)
        {
            var ddsId = identityService.GetIdentity(identifier);
            var coll = await cache.GetCached(
                CacheSeconds,
                CacheKeyPrefix + identifier,
                async () => await metsRepository.GetAsync(ddsId));
            return (ICollection)coll;
        }

        private async Task PopulateLastWriteTimes(ManifestationModel model)
        {
            // TODO - these paths should not be repeated here
            var identifier = model.DdsIdentifier;
            var textFileInfo = new S3StoredFileInfo(ddsOptions.TextContainer, $"raw/{identifier}", amazonS3);
            var imagesFileInfo = new S3StoredFileInfo(ddsOptions.AnnotationContainer,
                $"v3/{identifier}/images", amazonS3);
            var manifestFileInfo = new S3StoredFileInfo(ddsOptions.PresentationContainer, $"v3/{identifier}", amazonS3);

            await Task.WhenAll(textFileInfo.EnsureObjectMetadata(), imagesFileInfo.EnsureObjectMetadata(),
                manifestFileInfo.EnsureObjectMetadata());

            model.TextWriteTime = textFileInfo.GetLastWriteTime().Result;
            model.AnnotationWriteTime = imagesFileInfo.GetLastWriteTime().Result;
            model.ManifestWriteTime = manifestFileInfo.GetLastWriteTime().Result;
        }
    }
}