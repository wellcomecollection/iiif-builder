﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Ingest
{
    public class DashboardCloudServicesJobProcessor : IIngestJobProcessor
    {
        private readonly IDigitalObjectRepository digitalObjectRepository;
        private readonly IStatusProvider statusProvider;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly ILogger<DashboardCloudServicesJobProcessor> logger;

        private static readonly string[] SupportedFormats = new []
        {
            "image/jp2",
            "video/mpeg",
            "video/mp2",
            "video/mp4",
            "application/pdf",
            "audio/mp3",
            "audio/x-mpeg-3",
            "audio/wav",
            "audio/x-wav",
            "audio/mpeg",
        };

        const int MaximumSequentialFailuresTolerated = 3;

        public DashboardCloudServicesJobProcessor(
            IDigitalObjectRepository digitalObjectRepository,
            IStatusProvider statusProvider,
            DdsInstrumentationContext ddsInstrumentationContext,
            ILogger<DashboardCloudServicesJobProcessor> logger)
        {
            this.digitalObjectRepository = digitalObjectRepository;
            this.statusProvider = statusProvider;
            this.ddsInstrumentationContext = ddsInstrumentationContext;
            this.logger = logger;
        }

        public void UpdateStatus()
        {
            throw new NotImplementedException();
            // see body of this method in CloudServicesJobProcessor
        }

        public async Task ProcessQueue(int maxJobs = -1, bool usePriorityQueue = false, string? filter = null)
        {
            if (!await statusProvider.ShouldRunProcesses())
            {
                logger.LogWarning("Status provider returned false; will not process queue");
                return;
            }

            var jobs = ddsInstrumentationContext.DlcsIngestJobs
                .Where(j => j.StartProcessed == null);

            if (statusProvider.EarliestJobToTake.HasValue)
            {
                jobs = jobs.Where(j => j.Created >= statusProvider.EarliestJobToTake);
            }
            if (statusProvider.LatestJobToTake.HasValue)
            {
                jobs = jobs.Where(j => j.Created <= statusProvider.LatestJobToTake);
            }
            var jobsToProcess = jobs.OrderBy(j => j.Created).ToList();

            if (filter.HasText())
            {
                logger.LogInformation("Only processing identifiers that end with one of '{Filter}'", filter);
                var endings = filter.ToCharArray();
                logger.LogInformation("before filter, jobsToProcess has {JobCount} jobs", jobsToProcess.Count);
                jobsToProcess = jobsToProcess.Where(j => endings.Contains(j.Identifier[j.Identifier.Length - 1])).ToList();
                logger.LogInformation("After filter, jobsToProcess has {JobCount} jobs", jobsToProcess.Count);
            }
            if (maxJobs > -1)
            {
                jobsToProcess = jobsToProcess.Take(maxJobs).ToList();
            }

            int sequentialFailures = 0;

            if (jobsToProcess.Count > 0)
            {
                var lastHeartbeat = await statusProvider.GetHeartbeat() ?? DateTime.MinValue;
                foreach (DlcsIngestJob job in jobsToProcess)
                {
                    var now = DateTime.UtcNow; // use local variable rather than keep on reading file...
                    if ((now - lastHeartbeat).Seconds > 30)
                    {
                        await statusProvider.WriteHeartbeat();
                        lastHeartbeat = now;
                    }
                    if (!await statusProvider.ShouldRunProcesses())
                    {
                        logger.LogWarning("DDS status provider returned false; will not process queue");
                        return;
                    }
                    try
                    {
                        await ProcessJob(job, false, false, usePriorityQueue);
                        sequentialFailures = 0;
                    }
                    catch (Exception ex)
                    {
                        sequentialFailures++;
                        logger.LogError(ex, "Error in job queue");
                        if (sequentialFailures > MaximumSequentialFailuresTolerated)
                        {
                            var msg = string.Format("more than {0} errors in a row, rethrowing",
                                MaximumSequentialFailuresTolerated);
                            throw new ApplicationException(msg, ex);
                        }
                    }
                }
            }
        }

        public Task<ImageIngestResult> ProcessJob(DlcsIngestJob job, bool includeIngestingImages, bool forceReingest = false, bool usePriorityQueue = false) 
            => ProcessJob(job, image => includeIngestingImages, forceReingest, usePriorityQueue);

        /// <summary>
        /// Each job is a sequence from a b number
        /// That is, each job is a IIIF manifest.
        /// 
        /// </summary>
        /// <param name="job"></param>
        /// <param name="includeIngestingImage">Function to test any existing images that are marked as still ingesting, to determine
        /// whether they should be included in the job</param>
        /// <param name="forceReingest">Optional flag to force a complete re-ingest of the identifier</param>
        /// <param name="usePriorityQueue"></param>
        /// <returns></returns>
        public async Task<ImageIngestResult> ProcessJob(
            DlcsIngestJob job, 
            Func<Image, bool> includeIngestingImage, 
            bool forceReingest = false, 
            bool usePriorityQueue = false)
        {
            int jobId = job.Id;
            var dlcsCallContext = new DlcsCallContext("ProcessJob", jobId, job.Identifier);
            logger.LogDebug("Starting DlcsCallContext {callContext}", dlcsCallContext);
            logger.LogDebug("Processing DlcsIngestJob {jobId} with forceReingest={forceReingest}, priority={priority}",
                jobId, forceReingest, usePriorityQueue);
            // this should be ctx.DlcsIngestJobs.Single(j => j.Id == job.Id);
            var jobs = ddsInstrumentationContext.DlcsIngestJobs.Where(j => j.Id == jobId).ToList();
            if (jobs.Count == 1)
            {
                job = jobs[0];
                logger.LogInformation("ProcessJob: one found for {jobId}", jobId);
                job.StartProcessed = DateTime.UtcNow;
                await ddsInstrumentationContext.SaveChangesAsync();
            }
            else if (jobs.Count == 0)
            {
                logger.LogWarning("ProcessJob was passed a job but could not find it: {jobId}", jobId);
                return ImageIngestResult.Empty;
            }
            else
            {
                logger.LogWarning("ProcessJob was passed a job and found MORE THAN ONE: {jobId}", jobId);
                logger.LogWarning("Will process most recent");
                job = jobs.OrderByDescending(j => j.Created).First();
                job.StartProcessed = DateTime.UtcNow;
                await ddsInstrumentationContext.SaveChangesAsync();
            }

            // we expect a job to correspond to a manifestation
            IDigitalManifestation? digitalManifestation = null;
            Exception? error = null;
            string? errorDataMessage = null;
            IManifestation? manifestation = null;

            try
            {
                digitalManifestation = await digitalObjectRepository
                        .GetDigitalObject(job.GetManifestationIdentifier(), dlcsCallContext, false)
                    as IDigitalManifestation;
            }
            catch (Exception ex)
            {
                errorDataMessage = $"Error received during GetDigitalObject. Abandoning job {jobId} for {job.Identifier}.";
                error = ex;
            }

            if (digitalManifestation == null)
            {
                errorDataMessage = $"digitalManifestation is null for {job.Identifier} when processing job {jobId}";
            }
            else
            {
                manifestation = digitalManifestation.MetsManifestation;
                if (manifestation == null)
                {
                    errorDataMessage = $"digitalManifestation.MetsManifestation is null for {job.Identifier} in job {jobId}";
                }
                else if (!digitalManifestation.JobExactMatchForManifestation(job))
                {
                    errorDataMessage =
                        $"Job data doesn't match retrieved manifestation. Abandoning job {jobId} for {job.Identifier}";
                }
            }

            if (errorDataMessage.HasText() || manifestation == null || digitalManifestation == null)
            {
                logger.LogWarning("Problem retrieving DigitalManifestation during job {jobId}, error is {jobError}.", jobId, errorDataMessage);
                WriteErrorJobData(job.Id, errorDataMessage, error);
                return ImageIngestResult.Empty;
            }

            // how much do we move?
            // DashboardRepository doesn't record stuff to the DlcsIngestJob database.
            // The db is a queue for dashboard repository to do.
            // but it needs to surface data (json)_ for logging,...

            // assume that all the physical files in a manifestation are of the same type
            // (they might have FilePointers to different types, but the PhysicalFile element
            // will be, for example, TYPE="VIDEO"
            job = ddsInstrumentationContext.DlcsIngestJobs.Single(j => j.Id == job.Id);
            var assetType = manifestation.FirstInternetType;
            if (assetType != null)
            {
                // This doesn't really mean anything for born digital
                job.AssetType = assetType;
            }
            job.ImageCount = manifestation.SynchronisableFiles!.Count;
            logger.LogDebug("Job {jobId} has {assetCount} assets to process", jobId, job.ImageCount);
            await ddsInstrumentationContext.SaveChangesAsync();

            if (!JobCanBeProcessedNow(job, manifestation, out var deferReason))
            {
                logger.LogWarning("Job {jobId} cannot be processed now: {deferReason}, marking as deferred", 
                    jobId, deferReason);
                const string deferred = "deferred_format";
                job = ddsInstrumentationContext.DlcsIngestJobs.Single(j => j.Id == job.Id);
                job.EndProcessed = DateTime.UtcNow;
                job.Data = deferred;
                await ddsInstrumentationContext.SaveChangesAsync();
                return ImageIngestResult.Empty;
            }

            // TODO - consider any running processes....
            var syncOperation = await digitalObjectRepository.GetDlcsSyncOperation(digitalManifestation, true, dlcsCallContext);
            if (forceReingest)
            {
                int addCount = 0;
               // Forcing reingest, so make sure every image we haven't already identified for ingest up is added to the list
                foreach (var image in syncOperation.ImagesCurrentlyOnDlcs!.Values)
                {
                    if (image != null && !syncOperation.DlcsImagesToIngest!.Exists(im => im.StorageIdentifier == image.StorageIdentifier))
                    {
                        var newDlcsImage = syncOperation.ImagesThatShouldBeOnDlcs![image.StorageIdentifier!];
                        if (newDlcsImage == null)
                        {
                            logger.LogError("Forced reingest of asset that should not be on DLCS: {storageIdentifier}, callContext {callContext}", 
                                image.StorageIdentifier, dlcsCallContext.Id);
                        }
                        else
                        {
                            syncOperation.DlcsImagesToIngest.Add(newDlcsImage);
                            addCount++;
                        }
                    }
                }
                logger.LogDebug("ForceReingest added an additional {addCount} images to the ingest list", addCount);
            }
            else
            {
                // The caller can supply a function to test whether an image should be ingested even though it is still apparently ingesting from a previous job
                var ingestingImagesToIncludeInJob = syncOperation.DlcsImagesCurrentlyIngesting!.Where(includeIngestingImage).ToList();
                if (ingestingImagesToIncludeInJob.Any())
                {
                    logger.LogDebug("Adding an additional {addCount} already-ingesting images to the ingest list", ingestingImagesToIncludeInJob.Count);
                }
                syncOperation.DlcsImagesToIngest!.AddRange(ingestingImagesToIncludeInJob);
            }
            
            
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Sync Operation Summary:");
                foreach (var detail in syncOperation.GetSummary())
                {
                    logger.LogDebug(detail);
                }
            }

            var result = new ImageIngestResult();
            if (!syncOperation.HasInvalidAccessCondition)
            {
                // This test used to be  asset.Family != 'I' - is it safe to just use mediaType here? 
                // do we have any images whose mediaType does not start with image/* ?
                if (usePriorityQueue && syncOperation.DlcsImagesToIngest!.Any(asset => !asset.MediaType.IsImageMimeType()))
                {
                    usePriorityQueue = false;
                    logger.LogDebug("SyncOperation contains at least one non-image, so switching to regular queue");
                }
                
                await digitalObjectRepository.ExecuteDlcsSyncOperation(syncOperation, usePriorityQueue, dlcsCallContext);

                result.CloudBatchRegistrationResponse = syncOperation.Batches.ToArray();
                logger.LogDebug("Received {batchCount} batches back for syncOperation {syncOperationId}", 
                    result.CloudBatchRegistrationResponse.Length, syncOperation.SyncOperationIdentifier);

                var batchesForDb = new List<DlcsBatch>();
                if (syncOperation.BatchIngestOperationInfos.HasItems())
                {
                    batchesForDb.AddRange(syncOperation.BatchIngestOperationInfos);
                }
                if (syncOperation.BatchPatchOperationInfos.HasItems())
                {
                    batchesForDb.AddRange(syncOperation.BatchPatchOperationInfos);
                }
                if (batchesForDb.HasItems())
                {
                    // add the db batches back to the database
                    foreach (var batchOperationInfo in batchesForDb)
                    {
                        // give them the correct ID
                        batchOperationInfo.DlcsIngestJobId = job.Id;
                    }
                    await ddsInstrumentationContext.DlcsBatches.AddRangeAsync(batchesForDb);
                }
            }
            
            job = ddsInstrumentationContext.DlcsIngestJobs.Single(j => j.Id == job.Id);
            job.EndProcessed = DateTime.UtcNow;
            job.Succeeded = syncOperation.Succeeded;

            if (!syncOperation.Succeeded)
            {
                job.Data = syncOperation.Message;
            }
            await ddsInstrumentationContext.SaveChangesAsync();
            return result;
        }

        private static bool JobCanBeProcessedNow(DlcsIngestJob job, IManifestation manifestation, out string? reason)
        {
            if (job.AssetType.IsNullOrEmpty())
            {
                reason = "No job AssetType supplied";
                return false;
            }

            var ddsId = new DdsIdentifier(job.GetManifestationIdentifier());

            if (ddsId.HasBNumber)
            {
                bool isSupported = SupportedFormats.Contains(job.AssetType);
                reason = isSupported ? null : "Asset Type is not a supported format for Bnumber jobs";
                return isSupported;
            }
            
            // If it's not a b number, we'll just have to try and process it!
            // But we will make sure that EVERY file has a mimetype.
            if (manifestation.Sequence!.Any(pf => pf.MimeType.IsNullOrEmpty()))
            {
                reason = "One or more files are missing a MimeType";
                return false;
            }

            reason = null;
            return true;
        }

        private void WriteErrorJobData(int jobId, string? dataMessage, Exception? ex)
        {
            var job = ddsInstrumentationContext.DlcsIngestJobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                logger.LogError("WriteErrorJobData for {0}, job no longer in queue", jobId);
                return;
            }
            job.EndProcessed = DateTime.UtcNow;
            job.ImageCount = -1;
            job.Succeeded = false;
            if (dataMessage.HasText())
            {
                job.Data = dataMessage;
                if (ex != null)
                {
                    // abandon this job and raise error
                    job.Data += "\r\n" + ex.StackTrace;
                    logger.LogError(dataMessage, ex);
                }
            }
            ddsInstrumentationContext.SaveChanges();
        }


        // /// <summary>
        // /// This will probably no longer need to be called.
        // /// The Dashboard is where a user will go to investigate problems, and that will pull data directly from the DLCS.
        // /// 
        // /// So how do we know that something went wrong at the DLCS?
        // /// We can have recent ingests down the side, same as we do now.
        // /// 
        // /// But you'll have to go searching for problem items.
        // /// </summary>
        // /// <param name="job"></param>
        // private async Task UpdateJobAsync(DlcsIngestJob job)
        // {
        //     var digitisedManifestation = (await digitalObjectRepository
        //              .GetDigitalObject(job.GetManifestationIdentifier()))
        //              as IDigitalManifestation;
        //
        //     var query = new ImageQuery
        //     {
        //         String1 = job.Identifier,
        //         Number1 = job.SequenceIndex
        //     };
        //
        //
        //     logger.LogInformation("About to call GetImages for query String1={0} and Number1={1}", query.String1, query.Number1);
        //
        //     Debug.Assert(digitisedManifestation != null, "digitisedManifestation != null");
        //     var returnedImages = digitisedManifestation.DlcsImages!.ToList();
        //
        //     if (!returnedImages.HasItems())
        //     {
        //         job = ddsInstrumentationContext.DlcsIngestJobs.Single(j => j.Id == job.Id);
        //         job.Data = digitisedManifestation.DlcsStatus;
        //         await ddsInstrumentationContext.SaveChangesAsync();
        //         return;
        //     }
        //
        //     string? jobData = null;
        //     bool success = false;
        //     int readyImageCount = 0;
        //     if (returnedImages.Count == 0)
        //     {
        //         logger.LogInformation("No images returned");
        //         jobData = "no images";
        //     }
        //     else
        //     {
        //         readyImageCount = returnedImages.Count(IsFinished);
        //         if (returnedImages.Count == readyImageCount)
        //         {
        //             logger.LogInformation("All {0} images are ready", readyImageCount);
        //             success = true;
        //         }
        //         else
        //         {
        //             logger.LogInformation("{0} / {1} images in ready state", readyImageCount, returnedImages.Count);
        //             jobData = digitisedManifestation.DlcsResponse;
        //         }
        //     }
        //
        //     job = ddsInstrumentationContext.DlcsIngestJobs.Single(j => j.Id == job.Id);
        //     job.Data = jobData;
        //     job.ReadyImageCount = readyImageCount;
        //     job.Succeeded = success;
        //     await ddsInstrumentationContext.SaveChangesAsync();
        // }


        private static bool IsFinished(Image im)
        {
            return im.Finished.HasValue && im.Finished.Value.Year > 2000 && string.IsNullOrWhiteSpace(im.Error);
        }

    }
}