using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Utils.Logging;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    public class DigitalObjectRepository : IDigitalObjectRepository
    {
        private readonly ILogger<DigitalObjectRepository> logger;
        private readonly UriPatterns uriPatterns;
        private readonly IDlcs dlcs;
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly DdsOptions ddsOptions;

        public int DefaultSpace { get; }
        public int DefaultCustomer { get; }

        public DigitalObjectRepository(
            ILogger<DigitalObjectRepository> logger,
            UriPatterns uriPatterns,
            IDlcs dlcs,
            IMetsRepository metsRepository,
            DdsInstrumentationContext ddsInstrumentationContext,
            IOptions<DdsOptions> ddsOptions)
        {
            this.logger = logger;
            this.uriPatterns = uriPatterns;
            this.dlcs = dlcs;
            DefaultSpace = dlcs.DefaultSpace;
            DefaultCustomer = dlcs.DefaultCustomer;
            this.metsRepository = metsRepository;
            this.ddsInstrumentationContext = ddsInstrumentationContext;
            this.ddsOptions = ddsOptions.Value;
        }

        // make all the things, then hand back to DashboardCloudServicesJobProcessor process job.
        // the code that makes the calls to DLCS needs to go in here

        // and th sync...
        /// <summary>
        /// 
        /// </summary>
        /// <param name="identifier">Same as used for METS</param>
        /// <param name="dlcsCallContext"></param>
        /// <param name="includePdfDetails">If true, includes details of PDF with result. This is expensive, so avoid calling this if you don't need that information.</param>
        /// <returns></returns>
        public async Task<IDigitalObject> GetDigitalObject(
            DdsIdentifier identifier,
            DlcsCallContext dlcsCallContext,
            bool includePdfDetails = false)
        {
            logger.LogInformation("GetDigitalObject (mets+dlcs) for {identifier}", identifier);
            IDigitalObject digObject;
            var metsResource = await metsRepository.GetAsync(identifier);
            if (metsResource is IManifestation resource)
            {
                logger.LogDebug("{identifier} resolved to a Manifestation from METS", identifier);
                digObject = await MakeDigitalManifestation(resource, includePdfDetails, dlcsCallContext);
            }
            else if (metsResource is ICollection collection)
            {
                logger.LogDebug("{identifier} resolved to a Collection from METS", identifier);
                digObject = await MakeDigitalCollection(collection, includePdfDetails, dlcsCallContext);
            }
            else
            {
                throw new ArgumentException($"Cannot get a digital resource from METS for identifier {identifier}",
                    nameof(identifier));
            }

            digObject.Identifier = metsResource.Identifier;
            digObject.Partial = metsResource.Partial;

            //// DEBUG step - force evaluation of DLCS query
            //var list = (digResource as IDigitisedManifestation).DlcsImages.ToList();
            //foreach (var image in list)
            //{
            //    Log.DebugFormat("{0}/{1}", image.String1, image.Number2);
            //}
            return digObject;
        }

        public async Task ExecuteDlcsSyncOperation(
            SyncOperation syncOperation,
            bool usePriorityQueue,
            DlcsCallContext dlcsCallContext)
        {
            logger.LogDebug("Executing SyncOperation for {callContext}", dlcsCallContext);

            if (dlcsCallContext.SyncOperationId != syncOperation.SyncOperationIdentifier)
            {
                if (dlcsCallContext.SyncOperationId != null)
                {
                    throw new InvalidOperationException("The call context is not for this sync operation");
                }

                dlcsCallContext.SyncOperationId = syncOperation.SyncOperationIdentifier;
            }

            if (dlcs.PreventSynchronisation)
            {
                const string syncError =
                    @"Configuration prevents this application from synchronising with the DLCS. Is it a staging test environment for archive storage?";
                throw new InvalidOperationException(syncError);
            }

            if (syncOperation.MissingAccessConditions.HasItems())
            {
                throw new InvalidOperationException($"Cannot execute sync operation for {dlcsCallContext.Id} " +
                                                    $"because it contains {syncOperation.MissingAccessConditions.Count} " +
                                                    $"file(s) with missing access conditions");
            }
            
            var ingestOps = new List<Task>(2);
            logger.LogInformation(
                "Registering BATCH INGESTS for METS resource (manifestation) with Id {0}, context {callContext}",
                syncOperation.ManifestationIdentifier, dlcsCallContext);
            ingestOps.Add(DoBatchIngest(syncOperation.DlcsImagesToIngest, syncOperation, usePriorityQueue,
                dlcsCallContext));

            logger.LogInformation(
                "Registering BATCH PATCHES for METS resource (manifestation) with Id {0}, context {callContext}",
                syncOperation.ManifestationIdentifier, dlcsCallContext);
            ingestOps.Add(DoBatchPatch(syncOperation.DlcsImagesToPatch, syncOperation, dlcsCallContext));

            await Task.WhenAll(ingestOps);
            syncOperation.Succeeded = true;
            logger.LogInformation("Finished SyncOperation, context: {callContext}", dlcsCallContext);
        }

        /// <summary>
        /// Doesn't do a proper sync yet! just registers everything.
        /// 
        /// This needs to COMPARE mets data, and fix up.
        /// </summary>
        /// <param name="digitisedManifestation"></param>
        /// <param name="reIngestErrorImages"></param>
        /// <param name="dlcsCallContext"></param>
        /// <returns></returns>
        public async Task<SyncOperation> GetDlcsSyncOperation(
            IDigitalManifestation digitisedManifestation,
            bool reIngestErrorImages,
            DlcsCallContext dlcsCallContext)
        {
            logger.LogDebug("Will construct a SyncOperation for context: {callContext}", dlcsCallContext.Id);
            var metsManifestation = digitisedManifestation.MetsManifestation;
            var imagesAlreadyOnDlcs = digitisedManifestation.DlcsImages!.ToList();
            var missingAccessConditions = metsManifestation!.SynchronisableFiles.AnyItems()
                .Where(sf => sf.PhysicalFile!.AccessCondition == AccessCondition.Missing).ToList();
            logger.LogDebug(
                "There are {alreadyCount} images already on the DLCS for {identifier}, callContext: {callContext}",
                imagesAlreadyOnDlcs.Count, digitisedManifestation.Identifier, dlcsCallContext.Id);
            var syncOperation = new SyncOperation(dlcsCallContext)
            {
                ManifestationIdentifier = metsManifestation.Identifier,
                DlcsImagesCurrentlyIngesting = new List<Image>(),
                StorageIdentifiersToIgnore = metsManifestation.IgnoredStorageIdentifiers,
                ImagesCurrentlyOnDlcs =
                    await GetImagesExpectedOnDlcs(metsManifestation, imagesAlreadyOnDlcs, dlcsCallContext),
                ImagesThatShouldBeOnDlcs = new Dictionary<string, Image?>(),
                MissingAccessConditions = missingAccessConditions
            };

            /*
             ImagesCurrentlyOnDlcs is a map of what we think DLCS should have, to what it actually has.
             From this we can make lists - 
             what is missing, what is present but wrong metadata (needs patching), what is still ingesting
            */

            // What do we need to ingest? List of assets from METS that are not present on DLCS, or are present with transcoding errors
            var assetsToIngest = new List<IStoredFile>();
            logger.LogDebug("Deducing what assets we need to ingest, callContext {callContext}", dlcsCallContext.Id);
            foreach (var kvp in syncOperation.ImagesCurrentlyOnDlcs)
            {
                if (syncOperation.StorageIdentifiersToIgnore!.Contains(kvp.Key))
                {
                    // We do not want to sync this image with the DLCS.
                    logger.LogDebug("Ignoring {identifier}", kvp.Key);
                    continue;
                }

                var image = kvp.Value;
                if (image == null || (reIngestErrorImages && HasProblemRequiringReIngest(image)))
                {
                    assetsToIngest.Add(
                        metsManifestation.SynchronisableFiles!.Single(sf => sf.StorageIdentifier == kvp.Key));
                }
            }

            logger.LogDebug("We now have {assetCount} assets to ingest, callContext {callContext}",
                assetsToIngest.Count, dlcsCallContext.Id);

            // Get the manifestation level metadata that each image is going to need
            // NB This returns -1 for a Chemist and Druggist issue
            syncOperation.LegacySequenceIndex = await metsRepository.FindSequenceIndex(metsManifestation.Identifier);

            // This sets the default maxUnauthorised, before we know what the roles are. 
            // This is the default maxUnauthorised for the manifestation based only on on permittedOperations.
            // later we might override this for individual images - AND WHEN THE ASSET IS NOT AN IMAGE
            int maxUnauthorised = 1000;
            if (metsManifestation.PermittedOperations.Contains("wholeImageHighResAsJpg"))
            {
                maxUnauthorised = -1; // Use DLCS default max size
            }

            // What do we need to patch? List of existing DLCS images that don't have the correct metadata
            // Unlike the IStoredFiles in assetsToIngest, these are Hydra Images for the DLCS API
            syncOperation.DlcsImagesToIngest = new List<Image>();
            syncOperation.DlcsImagesToPatch = new List<Image>();
            syncOperation.Mismatches = new Dictionary<string, List<string>>();
            syncOperation.Orphans = imagesAlreadyOnDlcs
                .Where(image => !syncOperation.ImagesCurrentlyOnDlcs.ContainsKey(image.StorageIdentifier!)).ToList();
            logger.LogDebug("There are {orphanCount} orphan assets, callContext {callContext}",
                syncOperation.Orphans.Count, dlcsCallContext.Id);

            logger.LogDebug("Now work out what needs patching or ingesting");
            foreach (var storedFile in metsManifestation.SynchronisableFiles!)
            {
                if (syncOperation.StorageIdentifiersToIgnore!.Contains(storedFile.StorageIdentifier!))
                {
                    // We do not want to sync this image with the DLCS.
                    logger.LogDebug("We want to IGNORE {identifier}", storedFile.StorageIdentifier);
                    continue;
                }

                if (!AccessCondition.IsValid(storedFile.PhysicalFile!.AccessCondition))
                {
                    // This does not have an access condition that we can sync wth the DLCS
                    syncOperation.HasInvalidAccessCondition = true;
                    syncOperation.Message = "Sync operation found at least one invalid access condition";
                    logger.LogDebug("Asset {identifier} has an invalid access condition {accessCondition}",
                        storedFile.StorageIdentifier, storedFile.PhysicalFile!.AccessCondition);
                    continue;
                }

                var newDlcsImage = MakeDlcsImage(storedFile, metsManifestation.Identifier,
                    syncOperation.LegacySequenceIndex, maxUnauthorised);
                syncOperation.ImagesThatShouldBeOnDlcs[storedFile.StorageIdentifier!] = newDlcsImage;
                var existingDlcsImage = syncOperation.ImagesCurrentlyOnDlcs[storedFile.StorageIdentifier!];

                if (assetsToIngest.Contains(storedFile))
                {
                    syncOperation.DlcsImagesToIngest.Add(newDlcsImage);
                }
                else if (existingDlcsImage != null)
                {
                    // The DLCS already has this image...
                    var ingestDiffImage = GetIngestDiffImage(newDlcsImage, existingDlcsImage, out var ingestMismatchReasons);
                    if (ingestDiffImage != null)
                    {
                        // ...and it can't be batch-patched, we need to reingest it
                        syncOperation.DlcsImagesToIngest.Add(ingestDiffImage);
                        syncOperation.Mismatches[storedFile.StorageIdentifier!] = ingestMismatchReasons;
                    }

                    var patchDiffImage = GetPatchImage(newDlcsImage, existingDlcsImage, out var patchMismatchReasons);
                    if (patchDiffImage != null && ingestDiffImage == null)
                    {
                        // ...and it's a metadata change that can go in a batch patch
                        syncOperation.DlcsImagesToPatch.Add(patchDiffImage);
                        if (syncOperation.Mismatches.ContainsKey(storedFile.StorageIdentifier!))
                        {
                            syncOperation.Mismatches[storedFile.StorageIdentifier!].AddRange(patchMismatchReasons);
                        }
                        else
                        {
                            syncOperation.Mismatches[storedFile.StorageIdentifier!] = patchMismatchReasons;
                        }
                    }

                    if (existingDlcsImage.Ingesting == true)
                    {
                        // Add the NEW image, if there's a chance we might need to re submit this.
                        syncOperation.DlcsImagesCurrentlyIngesting.Add(newDlcsImage);
                    }
                }
            }

            logger.LogDebug("SyncOperation.DlcsImagesToIngest: {ingestCount}; callContext {callContext}",
                syncOperation.DlcsImagesToIngest.Count, dlcsCallContext.Id);
            logger.LogDebug("SyncOperation.DlcsImagesToPatch: {patchCount}; callContext {callContext}",
                syncOperation.DlcsImagesToPatch.Count, dlcsCallContext.Id);
            logger.LogDebug("SyncOperation.DlcsImagesCurrentlyIngesting: {ingestingCount}; callContext {callContext}",
                syncOperation.DlcsImagesCurrentlyIngesting.Count, dlcsCallContext.Id);

            return syncOperation;
        }


        private async Task<DigitalCollection> MakeDigitalCollection(
            ICollection metsCollection,
            bool includePdf,
            DlcsCallContext dlcsCallContext)
        {
            var dc = new DigitalCollection
            {
                MetsCollection = metsCollection,
                Identifier = metsCollection.Identifier
            };

            // There are currently 0 instances of an item with both collection + manifestation here.
            if (metsCollection.Collections.HasItems())
            {
                var collections = metsCollection.Collections
                    .Select(m => MakeDigitalCollection(m, includePdf, dlcsCallContext))
                    .ToList();

                await Task.WhenAll(collections);
                dc.Collections = collections.Select(c => c.Result);
            }

            if (metsCollection.Manifestations.HasItems())
            {
                var manifestations = metsCollection.Manifestations
                    .Select(m => MakeDigitalManifestation(m, includePdf, dlcsCallContext))
                    .ToList();
                await Task.WhenAll(manifestations);
                dc.Manifestations = manifestations.Select(m => m.Result);
            }

            return dc;
        }

        private async Task<DigitalManifestation> MakeDigitalManifestation(
            IManifestation metsManifestation,
            bool includePdf,
            DlcsCallContext dlcsCallContext)
        {
            var getDlcsImages = dlcs.GetImagesForString3(metsManifestation.Identifier, dlcsCallContext);
            var getPdf = includePdf ? dlcs.GetPdfDetails(metsManifestation.Identifier) : Task.FromResult<IPdf?>(null);

            await Task.WhenAll(getDlcsImages, getPdf);

            return new DigitalManifestation
            {
                MetsManifestation = metsManifestation,
                Identifier = metsManifestation.Identifier,
                DlcsImages = getDlcsImages.Result,
                PdfControlFile = getPdf.Result
            };
        }

        /// <summary>
        /// Images that match the metadata but are not in the METS
        /// </summary>
        /// <param name="imagesAlreadyOnDlcs"></param>
        /// <param name="dlcsImages"></param>
        /// <returns></returns>
        private List<Image> GetOrphans(Dictionary<string, Image> imagesAlreadyOnDlcs, IEnumerable<Image> dlcsImages)
        {
            return dlcsImages.Where(image => !imagesAlreadyOnDlcs.ContainsKey(image.StorageIdentifier!)).ToList();
        }

        public async Task<Batch?> GetBatch(string batchId, DlcsCallContext dlcsCallContext)
        {
            var batchOp = await dlcs.GetBatch(batchId, dlcsCallContext);
            return batchOp.ResponseObject;
        }

        private async Task<Dictionary<string, Image?>> GetImagesExpectedOnDlcs(
            IManifestation metsManifestation, List<Image> imagesAlreadyOnDlcs, DlcsCallContext dlcsCallContext)
        {
            // create an empty dictionary for all the images we need to have in the DLCS:
            var imagesExpectedOnDlcs = new Dictionary<string, Image?>();
            foreach (var storedFile in metsManifestation.SynchronisableFiles!)
            {
                imagesExpectedOnDlcs[storedFile.StorageIdentifier!] = null;
            }

            logger.LogDebug("We expect there to be {expectedCount} images in the DLCS. CallContext: {callContext}",
                imagesExpectedOnDlcs.Count, dlcsCallContext.Id);

            // go through all the DLCS images
            PopulateImagesExpectedOnDlcs(imagesExpectedOnDlcs, metsManifestation, imagesAlreadyOnDlcs);

            // do we have any local identifiers that the DLCS doesn't have?
            // If metadata has changed, our initial query might miss them, so we should fetch by IDs
            var missingDlcsImageIds = imagesExpectedOnDlcs
                .Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
            if (missingDlcsImageIds.Any())
            {
                logger.LogDebug("There are {missingCount} images missing from DLCS, callContext {callContext}",
                    missingDlcsImageIds.Count, dlcsCallContext);
                logger.LogDebug(
                    "We'll see if the DLCS has these IDs anyway, in the same space but maybe with different metadata");
                var mismatchedImages =
                    (await dlcs.GetImagesByDlcsIdentifiers(missingDlcsImageIds, dlcsCallContext)).ToList();
                logger.LogDebug(
                    "DLCS has {mismatchedCount} additional images with matching identifiers, callContext {callContext}",
                    mismatchedImages.Count, dlcsCallContext);
                PopulateImagesExpectedOnDlcs(imagesExpectedOnDlcs, metsManifestation, mismatchedImages);
            }

            return imagesExpectedOnDlcs;
        }

        public async Task<IEnumerable<Batch>> GetBatchesForImages(IEnumerable<Image?> images,
            DlcsCallContext dlcsCallContext)
        {
            var enumeratedImages = images.ToList();
            List<string> batchIds = new List<string>(enumeratedImages.Count);
            foreach (var image in enumeratedImages)
            {
                if (image != null && image.Batch.HasText() && !batchIds.Contains(image.Batch))
                {
                    // image.Batch is an integer from Deliverator but a URI from Protagonist
                    batchIds.Add(image.Batch);
                }
            }

            // TODO - batch the fetching of batches?
            var batches = new List<Batch>(batchIds.Count);
            if (batchIds.Count == 0)
            {
                logger.LogDebug("No batches exist for these images");
                return batches;
            }

            var debug = logger.IsEnabled(LogLevel.Debug);
            BatchMetrics? batchMetrics = debug ? new BatchMetrics() : null;

            foreach (var batchId in batchIds)
            {
                if (debug) batchMetrics!.BeginBatch();
                var batchOperation = await dlcs.GetBatch(batchId, dlcsCallContext);
                var batch = batchOperation.ResponseObject;
                if (batch != null) batches.Add(batch);
                if (debug) batchMetrics!.EndBatch(batch?.Count ?? -1);
            }

            if (debug)
            {
                logger.LogDebug("Timings for GetBatchesForImages: {summary}", batchMetrics!.Summary);
            }

            return batches;
        }


        private const string BatchPatchOperation = "Batch Patch Assets";
        private const string BatchIngestOperation = "Batch Ingest Assets";


        private async Task DoBatchPatch(List<Image>? dlcsImages, SyncOperation syncOperation,
            DlcsCallContext dlcsCallContext)
        {
            await DoDlcsBatchOperation(BatchPatchOperation, dlcsImages, syncOperation, dlcsCallContext);
        }

        private async Task DoBatchIngest(List<Image>? dlcsImages, SyncOperation syncOperation, bool priority,
            DlcsCallContext dlcsCallContext)
        {
            await DoDlcsBatchOperation(BatchIngestOperation, dlcsImages, syncOperation, dlcsCallContext, priority);
        }

        private async Task DoDlcsBatchOperation(
            string typeOfBatchOperation,
            List<Image>? dlcsImages,
            SyncOperation syncOperation,
            DlcsCallContext dlcsCallContext,
            bool priority = false)
        {
            // TODO - refactor DoBatchPatch and DoBatchIngest - They are 95% the same, they just wrap a different DLCS call
            if (dlcsImages.IsNullOrEmpty()) return;

            var debug = logger.IsEnabled(LogLevel.Debug);
            BatchMetrics? batchMetrics = debug ? new BatchMetrics() : null;

            foreach (var batch in dlcsImages.Batch(dlcs.BatchSize))
            {
                var imagesToSend = batch.ToArray();
                if (debug)
                {
                    batchMetrics!.BeginBatch(imagesToSend.Length);
                    logger.LogDebug("Batch {batchCounter}, length: {BatchLength}", batchMetrics.BatchCounter,
                        batchMetrics.BatchSize);
                }

                if (imagesToSend.Length == 0)
                {
                    logger.LogInformation("zero length - abandoning");
                    continue;
                }

                DlcsBatch batchForDlcs = new DlcsBatch
                {
                    BatchSize = imagesToSend.Length,
                    RequestSent = DateTime.Now
                };

                var imageRegistrationsAsHydraCollection = new HydraImageCollection
                {
                    Members = imagesToSend
                };

                if (typeOfBatchOperation == BatchPatchOperation)
                {
                    await CallDlcsPatchImages(syncOperation, dlcsCallContext, imageRegistrationsAsHydraCollection,
                        batchForDlcs);
                }
                else if (typeOfBatchOperation == BatchIngestOperation)
                {
                    await CallDlcsRegisterImages(syncOperation, dlcsCallContext, imageRegistrationsAsHydraCollection,
                        batchForDlcs, priority);
                }
                else
                {
                    throw new InvalidOperationException($"Batch Dlcs operation {typeOfBatchOperation} not recognised");
                }

                batchForDlcs.Finished = DateTime.Now;


                if (debug)
                {
                    batchMetrics!.EndBatch();
                    logger.LogDebug("Batch {batchCounter} took {batchTime} ms.", batchMetrics.BatchCounter,
                        batchMetrics.LastBatchTime);
                }
            }

            if (debug && batchMetrics!.BatchCounter > 0)
            {
                logger.LogDebug("Timings for '{operation}', {batchCounter} batches sent to DLCS for {callContext}: {summary}",
                    typeOfBatchOperation, batchMetrics.BatchCounter, dlcsCallContext.Id, batchMetrics.Summary);
            }
        }


        private async Task CallDlcsRegisterImages(
            SyncOperation syncOperation,
            DlcsCallContext dlcsCallContext,
            HydraImageCollection imageRegistrationsAsHydraCollection,
            DlcsBatch batchForDlcs,
            bool priority)
        {
            var registrationOperation =
                await dlcs.RegisterImages(imageRegistrationsAsHydraCollection, dlcsCallContext, priority);
            batchForDlcs.RequestBody = registrationOperation.RequestJson;
            batchForDlcs.ResponseBody = registrationOperation.ResponseJson;
            if (registrationOperation.Error != null)
            {
                batchForDlcs.ErrorCode = registrationOperation.Error.Status;
                batchForDlcs.ErrorText = registrationOperation.Error.Message;
            }

            syncOperation.BatchIngestOperationInfos.Add(batchForDlcs);
            syncOperation.Batches.Add(registrationOperation.ResponseObject!);
        }


        private async Task CallDlcsPatchImages(
            SyncOperation syncOperation,
            DlcsCallContext dlcsCallContext,
            HydraImageCollection imageRegistrationsAsHydraCollection,
            DlcsBatch batchForDlcs)
        {
            var registrationOperation = await dlcs.PatchImages(imageRegistrationsAsHydraCollection, dlcsCallContext);
            batchForDlcs.RequestBody = registrationOperation.RequestJson;
            batchForDlcs.ResponseBody = registrationOperation.ResponseJson;
            if (registrationOperation.Error != null)
            {
                batchForDlcs.ErrorCode = registrationOperation.Error.Status;
                batchForDlcs.ErrorText = registrationOperation.Error.Message;
            }

            syncOperation.BatchPatchOperationInfos.Add(batchForDlcs);
        }

        /// <summary> 
        /// return newDlcsImage if it differs from existingDlcsImage in a way that requires REINGEST
        /// </summary>
        /// <param name="newDlcsImage"></param>
        /// <param name="existingDlcsImage"></param>
        /// <param name="reasons"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private Image? GetIngestDiffImage(Image newDlcsImage, Image existingDlcsImage, out List<string> reasons)
        {
            reasons = new List<string>();
            Image? reingestImage = null;
            const string reingestMessageStructuredLoggingFormat =
                "Reingest required for {identifier}. Mismatch for {field} - new: {newValue}, existing: {existingValue}";
            const string reingestMessageUIFormat =
                "REINGEST because mismatch for {0} - new: '{1}', existing: '{2}'";
            if (existingDlcsImage.Origin != newDlcsImage.Origin)
            {
                logger.LogDebug(reingestMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "origin", newDlcsImage.Origin, existingDlcsImage.Origin);
                reasons.Add(string.Format(reingestMessageUIFormat,
                    "origin", newDlcsImage.Origin, existingDlcsImage.Origin));
                reingestImage ??= newDlcsImage;
            }

            // This one CURRENTLY requires reingest because it's engine that modifies the S3 location
            // But later it could go into regular batch-patch if protagonist no longer rejects it.
            if (existingDlcsImage.MaxUnauthorised != newDlcsImage.MaxUnauthorised)
            {
                logger.LogDebug(reingestMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "maxUnauthorised", newDlcsImage.MaxUnauthorised, existingDlcsImage.MaxUnauthorised);
                reasons.Add(string.Format(reingestMessageUIFormat,
                    "maxUnauthorised", newDlcsImage.MaxUnauthorised, existingDlcsImage.MaxUnauthorised));
                reingestImage ??= newDlcsImage;
            }

            // In DDS we never change these but they would need to be considered here
            // We also need to consider what's significant when either new or existing are null
            if (dlcs.SupportsDeliveryChannels)
            {
                // While we now set IOP to `use-original` for JP2s, we're relying on DLCS defaults for everything else still.
                // So we can't make this check just yet, because the DDS will assume no policy, which is a mismatch from 
                // the DLCS' assigned default policy (e.g., `video-max`)
                /*
                if (!StringUtils.EndWithSamePathElements(existingDlcsImage.ImageOptimisationPolicy, newDlcsImage.ImageOptimisationPolicy))
                {
                    logger.LogDebug(reingestMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                        "imageOptimisationPolicy", newDlcsImage.ImageOptimisationPolicy, existingDlcsImage.ImageOptimisationPolicy);
                    reasons.Add(string.Format(reingestMessageUIFormat,
                        "imageOptimisationPolicy", newDlcsImage.ImageOptimisationPolicy, existingDlcsImage.ImageOptimisationPolicy));
                    reingestImage ??= newDlcsImage;
                }
                */

                // We don't yet support editing the thumbnail policy, but when we do, it will look like this.
                // For that to work, the expected policy will need to be set in ProcessingBehaviour
                /*
                if (!StringUtils.EndWithSamePathElements(existingDlcsImage.ThumbnailPolicy, newDlcsImage.ThumbnailPolicy))
                {
                    logger.LogDebug(reingestMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                        "thumbnailPolicy", newDlcsImage.ThumbnailPolicy, existingDlcsImage.ThumbnailPolicy);
                    reasons.Add(string.Format(reingestMessageUIFormat,
                        "thumbnailPolicy", newDlcsImage.ThumbnailPolicy, existingDlcsImage.ThumbnailPolicy));
                    reingestImage ??= newDlcsImage;
                }
                */
                
                if (!AreEqual(existingDlcsImage.DeliveryChannels, newDlcsImage.DeliveryChannels))
                {
                    logger.LogDebug(reingestMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier,
                        "deliveryChannels", newDlcsImage.DeliveryChannels.ToCommaDelimitedList(), existingDlcsImage.DeliveryChannels.ToCommaDelimitedList());
                    reasons.Add(string.Format(reingestMessageUIFormat,
                        "deliveryChannels", newDlcsImage.DeliveryChannels.ToCommaDelimitedList(), existingDlcsImage.DeliveryChannels.ToCommaDelimitedList()));
                    reingestImage ??= newDlcsImage;
                }
            }

            return reingestImage;
        }


        /// <summary>
        /// return newDlcsImage if it differs from existingDlcsImage in a way that allows BATCH-PATCHing
        /// </summary>
        /// <param name="newDlcsImage"></param>
        /// <param name="existingDlcsImage"></param>
        /// <param name="reasons"></param>
        /// <returns></returns>
        private Image? GetPatchImage(Image newDlcsImage, Image existingDlcsImage, out List<string> reasons)
        {
            reasons = new List<string>();
            Image? patchImage = null;
            const string patchMessageStructuredLoggingFormat =
                "Patch required for {identifier}. Mismatch for {field} - new: {newValue}, existing: {existingValue}";
            const string patchMessageUIFormat =
                "PATCH because mismatch for {0} - new: {1}, existing: {2}";

            if (existingDlcsImage.String1 != newDlcsImage.String1)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "string1", newDlcsImage.String1, existingDlcsImage.String1);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "string1", newDlcsImage.String1, existingDlcsImage.String1));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.String2 != newDlcsImage.String2)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "string2", newDlcsImage.String2, existingDlcsImage.String2);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "string2", newDlcsImage.String2, existingDlcsImage.String2));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.String3 != newDlcsImage.String3)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "string3", newDlcsImage.String3, existingDlcsImage.String3);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "string3", newDlcsImage.String3, existingDlcsImage.String3));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.Number1 != newDlcsImage.Number1)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "number1", newDlcsImage.Number1, existingDlcsImage.Number1);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "number1", newDlcsImage.Number1, existingDlcsImage.Number1));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.Number2 != newDlcsImage.Number2)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "number2", newDlcsImage.Number2, existingDlcsImage.Number2);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "number2", newDlcsImage.Number2, existingDlcsImage.Number2));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.Number3 != newDlcsImage.Number3)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "number3", newDlcsImage.Number3, existingDlcsImage.Number3);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "number3", newDlcsImage.Number3, existingDlcsImage.Number3));
                patchImage ??= newDlcsImage;
            }

            if (existingDlcsImage.MediaType != newDlcsImage.MediaType)
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "mediaType", newDlcsImage.MediaType, existingDlcsImage.MediaType);
                reasons.Add(string.Format(patchMessageUIFormat,
                    "mediaType", newDlcsImage.MediaType, existingDlcsImage.MediaType));
                patchImage ??= newDlcsImage;
            }

            // Do we care about ordering?
            if (!AreEqual(existingDlcsImage.Tags, newDlcsImage.Tags))
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "tags", newDlcsImage.Tags.ToCommaDelimitedList(), existingDlcsImage.Tags.ToCommaDelimitedList());
                reasons.Add(string.Format(patchMessageUIFormat,
                    "tags", newDlcsImage.Tags.ToCommaDelimitedList(), existingDlcsImage.Tags.ToCommaDelimitedList()));
                patchImage ??= newDlcsImage;
            }

            if (!AreEqual(existingDlcsImage.Roles, newDlcsImage.Roles))
            {
                logger.LogDebug(patchMessageStructuredLoggingFormat, newDlcsImage.StorageIdentifier, 
                    "roles", newDlcsImage.Roles.ToCommaDelimitedList(), existingDlcsImage.Roles.ToCommaDelimitedList());
                reasons.Add(string.Format(patchMessageUIFormat,
                    "roles", newDlcsImage.Roles.ToCommaDelimitedList(), existingDlcsImage.Roles.ToCommaDelimitedList()));
                patchImage ??= newDlcsImage;
            }

            if (patchImage != null)
            {
                // remove fields not permitted in patch:
                patchImage.Origin = null;
                patchImage.MaxUnauthorised = null;
                patchImage.ImageOptimisationPolicy = null;
                patchImage.DeliveryChannels = null;
                // others?
            }

            return patchImage;
        }

        private bool AreEqual(string[]? s1, string[]? s2)
        {
            if (s1 == null)
            {
                return s2.IsNullOrEmpty();
            }

            if (s2 == null)
            {
                return s1.Length == 0;
            }

            return s1.SequenceEqual(s2);
        }


        private Image MakeDlcsImage(
            IStoredFile asset,
            DdsIdentifier ddsId,
            int sequenceIndex,
            int maxUnauthorised)
        {
            string origin = asset.GetStoredFileInfo().Uri;
            var imageRegistration = new Image
            {
                StorageIdentifier = asset.StorageIdentifier,
                ModelId = asset.StorageIdentifier, // will be patched to full path later
                Space = dlcs.DefaultSpace,
                Origin = origin,
                String1 = ddsId.PackageIdentifier, // will be string reference
                Number1 = sequenceIndex,
                Number2 = asset.PhysicalFile!.Index,
                MediaType = asset.MimeType,
                Family = (char)asset.Family
            };
            if (asset.PhysicalFile.RelativeAltoPath.HasText())
            {
                // TODO - Give the URI from where the DLCS can fetch this. Use the proper identifier not the seqIndex.
                imageRegistration.Text = asset.PhysicalFile.RelativeAltoPath;
                imageRegistration.TextType = "alto"; // also need a string to identify this as ALTO
            }

            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                case IdentifierType.NonBNumber: // for Archivematica, always (at present) a single manifestation
                    imageRegistration.String2 = ddsId.PackageIdentifier;
                    imageRegistration.String3 = ddsId.PackageIdentifier;
                    break;
                case IdentifierType.Volume:
                    imageRegistration.String2 = ddsId.VolumePart;
                    imageRegistration.String3 = ddsId.VolumePart;
                    break;
                case IdentifierType.Issue:
                    imageRegistration.String2 = ddsId.VolumePart;
                    imageRegistration.String3 = ddsId.IssuePart;
                    break;
                case IdentifierType.BNumberAndSequenceIndex:
                    // we should not get any like this
                    imageRegistration.Number1 = ddsId.SequenceIndex;
                    break;
            }

            var roles = GetRoles(asset.PhysicalFile);
            imageRegistration.Roles = roles;
            if (asset.Family == AssetFamily.Image)
            {
                imageRegistration.MaxUnauthorised = GetMaxUnauthorised(maxUnauthorised, roles);
            }
            else
            {
                imageRegistration.MaxUnauthorised = -1;
            }

            if (dlcs.SupportsDeliveryChannels)
            {
                var processing = asset.ProcessingBehaviour;
                imageRegistration.ImageOptimisationPolicy = processing.ImageOptimisationPolicy;
                imageRegistration.DeliveryChannels = processing.DeliveryChannels.ToArray();
                imageRegistration.Family = null;
            }
            return imageRegistration;
        }
  
        public async Task<IEnumerable<DlcsIngestJob>> GetMostRecentIngestJobs(string identifier, int number)
        {
            // int sequenceIndex = await metsRepository.FindSequenceIndex(identifier);
            var jobQuery = GetJobQuery(identifier); //, legacySequenceIndex: sequenceIndex);
            if (jobQuery == null)
            {
                return Array.Empty<DlcsIngestJob>();
            }

            return await jobQuery
                .Include(j => j.DlcsBatches)
                .OrderByDescending(j => j.Created)
                .Take(number)
                .ToListAsync();
        }

        public async Task<JobActivity> GetRationalisedJobActivity(SyncOperation syncOperation,
            DlcsCallContext dlcsCallContext)
        {
            var batchesForImages =
                await GetBatchesForImages(syncOperation.ImagesCurrentlyOnDlcs!.Values, dlcsCallContext);
            var imageBatches = batchesForImages.ToList();
            // DASH-46
            if (syncOperation.RequiresSync == false &&
                imageBatches.Any(b => b.Superseded == false && (b.Completed != b.Count)))
            {
                // Some of these batches may seem incomplete, but they have been superseded
                imageBatches = await dlcs.GetTestedImageBatches(imageBatches, dlcsCallContext);
            }

            var updatedJobs = GetUpdatedIngestJobs(syncOperation, imageBatches).ToList();
            return new JobActivity(imageBatches, updatedJobs);
        }

        public async Task<int> RemoveOldJobs(string id)
        {
            // int sequenceIndex = await metsRepository.FindSequenceIndex(id);
            var jobQuery = GetJobQuery(id); // , legacySequenceIndex: sequenceIndex);
            if (jobQuery == null)
            {
                return 0;
            }

            var jobs = await jobQuery.OrderByDescending(j => j.Created).Skip(1).ToListAsync();
            int num = jobs.Count;
            if (num > 0)
            {
                foreach (var jobToRemove in jobs)
                {
                    ddsInstrumentationContext.DlcsIngestJobs.Remove(jobToRemove);
                }

                await ddsInstrumentationContext.SaveChangesAsync();
            }

            return num;
        }


        /// <summary>
        /// If syncoperation is OK, then mark old jobs as succeeded.
        /// BUT they must not have any active Batches - no ongoing jobs!
        /// 
        /// Can delete a set of jobs
        /// </summary>
        /// <param name="syncOperation"></param>
        /// <param name="activeBatches"></param>
        /// <returns></returns>
        private IEnumerable<DlcsIngestJob> GetUpdatedIngestJobs(SyncOperation syncOperation, List<Batch> activeBatches)
        {
            var jobQuery =
                GetJobQuery(syncOperation
                    .ManifestationIdentifier!); // , legacySequenceIndex: syncOperation.LegacySequenceIndex);
            if (jobQuery == null)
            {
                return Array.Empty<DlcsIngestJob>();
            }

            var jobs = jobQuery
                .Include(j => j.DlcsBatches)
                .OrderByDescending(j => j.Created)
                .ToList();
            if (syncOperation.RequiresSync == false && !activeBatches.HasItems())
            {
                bool flag = false;
                foreach (var job in jobs)
                {
                    if (job.EndProcessed.HasValue && job.Succeeded == false)
                    {
                        flag = true;
                        job.Succeeded = true;
                    }
                }

                if (flag)
                {
                    ddsInstrumentationContext.SaveChanges();
                }
            }

            return jobs;
        }

        private IQueryable<DlcsIngestJob>? GetJobQuery(string identifier) // , int legacySequenceIndex = -1)
        {
            var ddsId = new DdsIdentifier(identifier);
            IQueryable<DlcsIngestJob>? jobQuery = null;
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                    jobQuery = ddsInstrumentationContext.DlcsIngestJobs
                        .Where(j => j.Identifier == identifier);
                    break;
                case IdentifierType.Volume:
                    //int sequenceIndex = metsRepository.FindSequenceIndex(identifier);
                    jobQuery = ddsInstrumentationContext.DlcsIngestJobs
                        .Where(j => (j.VolumePart != null && j.VolumePart == identifier));
                    // || (j.VolumePart == null && j.Identifier == ddsId.BNumber && j.SequenceIndex == legacySequenceIndex));
                    break;
                case IdentifierType.BNumberAndSequenceIndex:
                    jobQuery = ddsInstrumentationContext.DlcsIngestJobs
                        .Where(j => j.Identifier == ddsId.PackageIdentifier && j.SequenceIndex == ddsId.SequenceIndex);
                    break;
                case IdentifierType.Issue:
                    jobQuery = ddsInstrumentationContext.DlcsIngestJobs
                        .Where(j => j.IssuePart == identifier);
                    break;
            }

            return jobQuery;
        }


        private bool HasProblemRequiringReIngest(Image dlcsImage)
        {
            if (dlcsImage.Error.HasText())
            {
                logger.LogDebug(
                    "Image {identifier} has an error and requires reingest. Error stored in DLCS is '{error}'",
                    dlcsImage.Id, dlcsImage.Error);
                return true;
            }

            return false;
        }

        private void PopulateImagesExpectedOnDlcs(
            Dictionary<string, Image?> imageDictionary,
            IManifestation thisManifestation,
            IEnumerable<Image> imagesAlreadyOnDlcs)
        {
            foreach (var dlcsImage in imagesAlreadyOnDlcs)
            {
                var physFile = thisManifestation.SynchronisableFiles!.SingleOrDefault(
                    sf => sf.StorageIdentifier == dlcsImage.StorageIdentifier);
                if (physFile != null)
                {
                    // this DLCS image belongs in the dictionary
                    imageDictionary[dlcsImage.StorageIdentifier!] = dlcsImage;
                }
            }
        }

        private string[] GetRoles(IPhysicalFile asset)
        {
            if (asset.AccessCondition.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Physical File is missing Access Condition");
            }

            if (asset.AccessCondition == AccessCondition.Open)
            {
                return Array.Empty<string>();
            }

            var acUri = dlcs.GetRoleUri(asset.AccessCondition);
            logger.LogInformation("Asset will be registered with role {0}", acUri);
            return new[] { acUri };
        }

        private string? reqRegUri;

        private int GetMaxUnauthorised(int sequenceMaxSize, string[] roles)
        {
            if (!roles.HasItems()) return sequenceMaxSize;
            reqRegUri ??= dlcs.GetRoleUri(AccessCondition.RequiresRegistration);
            return roles.Contains(reqRegUri) ? 200 : 0;
        }

        public Task<IEnumerable<ErrorByMetadata>> GetErrorsByMetadata(DlcsCallContext dlcsCallContext)
            => dlcs.GetErrorsByMetadata(dlcsCallContext);

        public Task<Page<ErrorByMetadata>> GetErrorsByMetadata(int page, DlcsCallContext dlcsCallContext)
            => dlcs.GetErrorsByMetadata(page, dlcsCallContext);

        ///// <summary>
        ///// This could be removed once alto search is replaced.
        ///// </summary>
        ///// <param name="identifier"></param>
        ///// <returns></returns>
        public async Task<int> FindSequenceIndex(string identifier)
        {
            return await metsRepository.FindSequenceIndex(identifier);
        }

        public Task<bool> DeletePdf(string identifier) => dlcs.DeletePdf(identifier);

        public async Task<int> DeleteOrphans(string id, DlcsCallContext dlcsCallContext)
        {
            var manifestation = await GetDigitalObject(id, dlcsCallContext) as IDigitalManifestation;
            var syncOperation = await GetDlcsSyncOperation(manifestation!, false, dlcsCallContext);
            return await dlcs.DeleteImages(syncOperation.Orphans!, dlcsCallContext);
        }

        public IngestAction LogAction(string manifestationId, int? jobId, string userName, string action,
            string? description = null)
        {
            var ia = new IngestAction
            {
                Action = action,
                Description = description,
                JobId = jobId,
                ManifestationId = manifestationId,
                Performed = DateTime.Now,
                Username = userName
            };
            ddsInstrumentationContext.IngestActions.Add(ia);
            ddsInstrumentationContext.SaveChanges();
            return ia;
        }

        public IEnumerable<IngestAction> GetRecentActions(int count, string? user = null)
        {
            IQueryable<IngestAction> q = ddsInstrumentationContext
                .IngestActions.OrderByDescending(ia => ia.Id);
            if (user.HasText())
            {
                q = q.Where(ia => ia.Username == user);
            }

            return q.Take(count).ToList();
        }

        public Task<Dictionary<string, long>> GetDlcsQueueLevel() => dlcs.GetDlcsQueueLevel();

        public AVDerivative[] GetAVDerivatives(IDigitalManifestation digitisedManifestation)
        {
            var derivs = new List<AVDerivative>();
            if (digitisedManifestation.MetsManifestation!.Type is "Video" or "Audio")
            {
                foreach (var asset in digitisedManifestation.DlcsImages!)
                {
                    derivs.AddRange(GetAVDerivatives(asset));
                }
            }

            return derivs.ToArray();
        }
        
        
        public DeliveredFile[] GetDeliveredFiles(IPhysicalFile physicalFile)
        {
            return GetDeliveredFiles(physicalFile.GetDefaultFile());
        }
    
        public DeliveredFile[] GetDeliveredFiles(IStoredFile? storedFile)
        {
            if (storedFile == null)
            {
                return Array.Empty<DeliveredFile>();
            }

            var deliveredFiles = new List<DeliveredFile>();
            DeliveredFile? file = null;

            if (dlcs.SupportsDeliveryChannels)
            {
                var behaviour = storedFile.ProcessingBehaviour;
                if (behaviour.DeliveryChannels.Contains("file"))
                {
                    file = new DeliveredFile
                    {
                        DeliveryChannel = "file",
                        PublicUrl = uriPatterns.DlcsFile(dlcs.ResourceEntryPoint, storedFile.StorageIdentifier),
                        MediaType = storedFile.MimeType
                    };
                    file.DlcsUrl = file.PublicUrl.Replace(
                        $"{dlcs.ResourceEntryPoint}file/",
                        $"{dlcs.InternalResourceEntryPoint}file/{dlcs.DefaultCustomer}/{dlcs.DefaultSpace}/");
                    deliveredFiles.Add(file);
                }

                switch (storedFile.Family)
                {
                    case AssetFamily.Image:
                        if (behaviour.DeliveryChannels.Contains("iiif-img"))
                        {
                            var imgService = new DeliveredFile
                            {
                                DeliveryChannel = "iiif-img",
                                PublicUrl = uriPatterns.DlcsImageService(dlcs.ResourceEntryPoint,
                                    storedFile.StorageIdentifier),
                                MediaType = "iiif/image",
                                Width = storedFile.AssetMetadata!.GetImageWidth(),
                                Height = storedFile.AssetMetadata.GetImageHeight()
                            };
                            imgService.DlcsUrl = imgService.PublicUrl.Replace(
                                $"{dlcs.ResourceEntryPoint}image/",
                                $"{dlcs.InternalResourceEntryPoint}iiif-img/{dlcs.DefaultCustomer}/{dlcs.DefaultSpace}/");
                            deliveredFiles.Add(imgService);
                            
                            // TODO - thumbs as separate channel
                        }

                        if (file != null)
                        {
                            file.Width = storedFile.AssetMetadata!.GetImageWidth();
                            file.Height = storedFile.AssetMetadata.GetImageHeight();
                        }
                        break;
                    
                    case AssetFamily.TimeBased:
                        if (storedFile.MimeType.IsAudioMimeType())
                        {
                            if (file != null)
                            {
                                file.Duration = storedFile.AssetMetadata!.GetDuration();
                            }
                            if (behaviour.DeliveryChannels.Contains("iiif-av"))
                            {
                                var mp3 = new DeliveredFile
                                {
                                    DeliveryChannel = "iiif-av",
                                    PublicUrl = uriPatterns.DlcsAudio(dlcs.ResourceEntryPoint,
                                        storedFile.StorageIdentifier, "mp3"),
                                    MediaType = "audio/mp3",
                                    Duration = storedFile.AssetMetadata!.GetDuration()
                                };
                                mp3.DlcsUrl = mp3.PublicUrl.Replace(
                                    $"{dlcs.ResourceEntryPoint}av/",
                                    $"{dlcs.InternalResourceEntryPoint}iiif-av/{dlcs.DefaultCustomer}/{dlcs.DefaultSpace}/");
                                deliveredFiles.Add(mp3);
                            }
                        }
                        else if (storedFile.MimeType.IsVideoMimeType())
                        {
                            if (file != null)
                            {
                                file.Duration = storedFile.AssetMetadata!.GetDuration();
                                file.Width = storedFile.AssetMetadata.GetImageWidth();
                                file.Height = storedFile.AssetMetadata.GetImageHeight();
                            }
                            if (behaviour.DeliveryChannels.Contains("iiif-av"))
                            {
                                var mp4 = new DeliveredFile
                                {
                                    DeliveryChannel = "iiif-av",
                                    PublicUrl = uriPatterns.DlcsVideo(dlcs.ResourceEntryPoint,
                                        storedFile.StorageIdentifier, "mp4"),
                                    MediaType = "video/mp4",
                                    Duration = storedFile.AssetMetadata!.GetDuration(),
                                    Width = storedFile.AssetMetadata.GetImageWidth(),
                                    Height = storedFile.AssetMetadata.GetImageHeight()
                                };
                                mp4.DlcsUrl = mp4.PublicUrl.Replace(
                                    $"{dlcs.ResourceEntryPoint}av/",
                                    $"{dlcs.InternalResourceEntryPoint}iiif-av/{dlcs.DefaultCustomer}/{dlcs.DefaultSpace}/");
                                deliveredFiles.Add(mp4);
                            }
                            
                        }
                        break;
                }
                
            }

            return deliveredFiles.ToArray();
        }



        public List<AVDerivative> GetAVDerivatives(Image dlcsAsset)
        {
            // This knows that we have webm, mp4 and mp3... it shouldn't know this, it should learn it.
            var derivatives = new List<AVDerivative>();
            if (dlcsAsset.MediaType!.StartsWith("video"))
            {
                derivatives.Add(MakeDerivative(uriPatterns.DlcsVideo, dlcsAsset, "mp4"));
                derivatives.Add(MakeDerivative(uriPatterns.DlcsVideo, dlcsAsset, "webm"));
            }

            if (dlcsAsset.MediaType.Contains("audio"))
            {
                derivatives.Add(MakeDerivative(uriPatterns.DlcsAudio, dlcsAsset, "mp3"));
            }

            return derivatives;
        }

        private AVDerivative MakeDerivative(Func<string, string?, string, string> pattern, Image dlcsAsset,
            string fileExt)
        {
            var publicUrl = pattern(dlcs.ResourceEntryPoint, dlcsAsset.StorageIdentifier, fileExt);
            var internalUrl = publicUrl.Replace(
                $"{dlcs.ResourceEntryPoint}av/",
                $"{dlcs.InternalResourceEntryPoint}iiif-av/{dlcs.DefaultCustomer}/{dlcs.DefaultSpace}/");
            return new AVDerivative(publicUrl, internalUrl, fileExt);
        }
    }
}