using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
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

        public int DefaultSpace { get; }

        public DigitalObjectRepository(
            ILogger<DigitalObjectRepository> logger,
            UriPatterns uriPatterns,
            IDlcs dlcs, 
            IMetsRepository metsRepository,
            DdsInstrumentationContext ddsInstrumentationContext)
        {
            this.logger = logger;
            this.uriPatterns = uriPatterns;
            this.dlcs = dlcs;
            DefaultSpace = dlcs.DefaultSpace;
            this.metsRepository = metsRepository;
            this.ddsInstrumentationContext = ddsInstrumentationContext;
        }

        // make all the things, then hand back to DashboarcCloudServicesJobProcessor process job.
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

            var ingestOps = new List<Task>(2);
            logger.LogInformation("Registering BATCH INGESTS for METS resource (manifestation) with Id {0}, context {callContext}",
                syncOperation.ManifestationIdentifier, dlcsCallContext);
            ingestOps.Add(DoBatchIngest(syncOperation.DlcsImagesToIngest, syncOperation, usePriorityQueue, dlcsCallContext));

            logger.LogInformation("Registering BATCH PATCHES for METS resource (manifestation) with Id {0}, context {callContext}",
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
            logger.LogDebug("There are {alreadyCount} images already on the DLCS for {identifier}, callContext: {callContext}",
                imagesAlreadyOnDlcs.Count, digitisedManifestation.Identifier, dlcsCallContext.Id);
            var syncOperation = new SyncOperation(dlcsCallContext)
            {
                ManifestationIdentifier = metsManifestation!.Identifier,
                DlcsImagesCurrentlyIngesting = new List<Image>(),
                StorageIdentifiersToIgnore = metsManifestation.IgnoredStorageIdentifiers,
                ImagesExpectedOnDlcs = await GetImagesExpectedOnDlcs(metsManifestation, imagesAlreadyOnDlcs, dlcsCallContext)
            };

            /*
             ImagesExpectedOnDlcs is a map of what we think DLCS should have, to what it actually has.
             From this we can make lists - 
             what is missing, what is present but wrong metadata (needs patching), what is still ingesting
            */
            
            // What do we need to ingest? List of assets from METS that are not present on DLCS, or are present with transcoding errors
            var assetsToIngest = new List<IStoredFile>();
            logger.LogDebug("Deducing what assets we need to ingest, callContext {callContext}", dlcsCallContext.Id);
            foreach (var kvp in syncOperation.ImagesExpectedOnDlcs)
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
                    assetsToIngest.Add(metsManifestation.SynchronisableFiles!.Single(sf => sf.StorageIdentifier == kvp.Key));
                }
            }
            logger.LogDebug("We now have {assetCount} assets to ingest, callContext {callContext}", 
                assetsToIngest.Count, dlcsCallContext.Id);

            // Get the manifestation level metadata that each image is going to need
            // NB This returns -1 for a Chemist and Druggist issue
            syncOperation.LegacySequenceIndex = await metsRepository.FindSequenceIndex(metsManifestation.Identifier);

            // This sets the default maxUnauthorised, before we know what the roles are. 
            // This is the default maxUnauthorised for the manifestation based only on on permittedOperations.
            // later we might override this for individual images.
            int maxUnauthorised = 1000;
            if (metsManifestation.PermittedOperations.Contains("wholeImageHighResAsJpg"))
            {
                maxUnauthorised = -1; // Use DLCS default max size
            }

            // What do we need to patch? List of existing DLCS images that don't have the correct metadata
            // Unlike the IStoredFiles in assetsToIngest, these are Hydra Images for the DLCS API
            syncOperation.DlcsImagesToIngest = new List<Image>();
            syncOperation.DlcsImagesToPatch = new List<Image>();
            syncOperation.Orphans = imagesAlreadyOnDlcs.Where(image => ! syncOperation.ImagesExpectedOnDlcs.ContainsKey(image.StorageIdentifier!)).ToList();
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
                
                var newDlcsImage = MakeDlcsImage(storedFile, metsManifestation.Identifier, syncOperation.LegacySequenceIndex, maxUnauthorised);
                var existingDlcsImage = syncOperation.ImagesExpectedOnDlcs[storedFile.StorageIdentifier!];

                if (assetsToIngest.Contains(storedFile))
                {
                    syncOperation.DlcsImagesToIngest.Add(newDlcsImage);
                }
                else if (existingDlcsImage != null)
                {
                    // The DLCS already has this image
                    var patchDiffImage = GetPatchImage(newDlcsImage, existingDlcsImage);
                    if (patchDiffImage != null)
                    {
                        // and we need to patch it
                        syncOperation.DlcsImagesToPatch.Add(patchDiffImage);
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
                logger.LogDebug("We'll see if the DLCS has these IDs anyway, in the same space but maybe with different metadata");
                var mismatchedImages = (await dlcs.GetImagesByDlcsIdentifiers(missingDlcsImageIds, dlcsCallContext)).ToList();
                logger.LogDebug("DLCS has {mismatchedCount} additional images with matching identifiers, callContext {callContext}",
                    mismatchedImages.Count, dlcsCallContext);
                PopulateImagesExpectedOnDlcs(imagesExpectedOnDlcs, metsManifestation, mismatchedImages);
            }
            return imagesExpectedOnDlcs;
        }

        public async Task<IEnumerable<Batch>> GetBatchesForImages(IEnumerable<Image?> images, DlcsCallContext dlcsCallContext)
        {
            var enumeratedImages = images.ToList();
            List<string> batchIds = new List<string>(enumeratedImages.Count);
            foreach (var image in enumeratedImages)
            {
                if (image != null && image.Batch.HasText() && !batchIds.Contains(image.Batch))
                {
                    batchIds.Add(image.Batch);
                }
            }

            // TODO - batch the fetching of batches?
            var batches = new List<Batch>(batchIds.Count);
            foreach (var batchId in batchIds)
            {
                var b = await GetBatch(batchId, dlcsCallContext);
                if (b != null) batches.Add(b);
            }
            // DLCS image.batch is just an ID, not a URI; these needs to be fixed to improve this code...
            return batches;
        }

        private async Task DoBatchPatch(List<Image>? dlcsImagesToPatch, SyncOperation syncOperation, DlcsCallContext dlcsCallContext)
        {
            if (dlcsImagesToPatch.IsNullOrEmpty()) return;
            
            // TODO - refactor this and DoBatchIngest - They use a different kind of Operation
            foreach (var batch in dlcsImagesToPatch.Batch(dlcs.BatchSize))
            {
                var imagePatches = batch.ToArray();
                logger.LogDebug("Batch of {BatchLength}", imagePatches.Length);

                if (imagePatches.Length == 0)
                {
                    logger.LogInformation("zero length - abandoning");
                    continue;
                }

                DlcsBatch dbBatchPatch = new DlcsBatch
                {
                    BatchSize = imagePatches.Length,
                    RequestSent = DateTime.Now
                };

                var imageRegistrationsAsHydraCollection = new HydraImageCollection
                {
                    Members = imagePatches
                };
                var registrationOperation = await dlcs.PatchImages(imageRegistrationsAsHydraCollection, dlcsCallContext);
                dbBatchPatch.Finished = DateTime.Now;
                dbBatchPatch.RequestBody = registrationOperation.RequestJson;
                dbBatchPatch.ResponseBody = registrationOperation.ResponseJson;
                if (registrationOperation.Error != null)
                {
                    dbBatchPatch.ErrorCode = registrationOperation.Error.Status;
                    dbBatchPatch.ErrorText = registrationOperation.Error.Message;
                }
                syncOperation.BatchPatchOperationInfos.Add(dbBatchPatch);
            }
        }

        private async Task DoBatchIngest(List<Image>? dlcsImagesToIngest, SyncOperation syncOperation, bool priority, DlcsCallContext dlcsCallContext)
        {
            if (dlcsImagesToIngest.IsNullOrEmpty()) return;
            
            foreach (var batch in dlcsImagesToIngest.Batch(dlcs.BatchSize))
            {
                var imageRegistrations = batch.ToArray();
                logger.LogDebug("Batch of {BatchLength}", imageRegistrations.Length);

                if (imageRegistrations.Length == 0)
                {
                    logger.LogInformation("zero length - abandoning");
                    continue;
                }

                DlcsBatch dbDlcsBatch = new DlcsBatch
                {
                    BatchSize = imageRegistrations.Length,
                    RequestSent = DateTime.Now
                };

                var imageRegistrationsAsHydraCollection = new HydraImageCollection
                {
                    Members = imageRegistrations
                };
                var registrationOperation = await dlcs.RegisterImages(imageRegistrationsAsHydraCollection, dlcsCallContext, priority);
                dbDlcsBatch.Finished = DateTime.Now;
                dbDlcsBatch.RequestBody = registrationOperation.RequestJson;
                dbDlcsBatch.ResponseBody = registrationOperation.ResponseJson;
                if (registrationOperation.Error != null)
                {
                    dbDlcsBatch.ErrorCode = registrationOperation.Error.Status;
                    dbDlcsBatch.ErrorText = registrationOperation.Error.Message;
                }
                syncOperation.BatchIngestOperationInfos.Add(dbDlcsBatch);
                syncOperation.Batches.Add(registrationOperation.ResponseObject!);
            }
        }

        /// <summary>
        /// return newDlcsImage if it differs from existingDlcsImage in a way that requires patching
        /// </summary>
        /// <param name="newDlcsImage"></param>
        /// <param name="existingDlcsImage"></param>
        /// <returns></returns>
        private Image? GetPatchImage(Image newDlcsImage, Image existingDlcsImage)
        {
            //if (existingDlcsImage.ImageOptimisationPolicy != newDlcsImage.ImageOptimisationPolicy)
            //    return newDlcsImage;
            //if (existingDlcsImage.ThumbnailPolicy != newDlcsImage.ThumbnailPolicy)
            //    return newDlcsImage;

            Image? patchImage = null;
            const string patchMessageFormat =
                "Patch required for {identifier}. Mismatch for {field} - new: {newValue}, existing: {existingValue}";
            
            if (existingDlcsImage.Origin != newDlcsImage.Origin)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "origin", newDlcsImage.Origin, existingDlcsImage.Origin);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.String1 != newDlcsImage.String1)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "string1", newDlcsImage.String1, existingDlcsImage.String1);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.String2 != newDlcsImage.String2)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "string2", newDlcsImage.String2, existingDlcsImage.String2);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.String3 != newDlcsImage.String3)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "string3", newDlcsImage.String3, existingDlcsImage.String3);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.Number1 != newDlcsImage.Number1)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "number1", newDlcsImage.Number1, existingDlcsImage.Number1);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.Number2 != newDlcsImage.Number2)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "number2", newDlcsImage.Number2, existingDlcsImage.Number2);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.Number3 != newDlcsImage.Number3)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "number3", newDlcsImage.Number3, existingDlcsImage.Number3);
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.MediaType != newDlcsImage.MediaType)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "mediaType", newDlcsImage.MediaType, existingDlcsImage.MediaType);
                patchImage ??= newDlcsImage;
            }

            // Do we care about ordering?
            if (!AreEqual(existingDlcsImage.Tags, newDlcsImage.Tags))
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "tags", newDlcsImage.Tags.ToCommaDelimitedList(), existingDlcsImage.Tags.ToCommaDelimitedList());
                patchImage ??= newDlcsImage;
            }
            if (!AreEqual(existingDlcsImage.Roles, newDlcsImage.Roles))
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "roles", newDlcsImage.Roles.ToCommaDelimitedList(), existingDlcsImage.Roles.ToCommaDelimitedList());
                patchImage ??= newDlcsImage;
            }
            if (existingDlcsImage.MaxUnauthorised != newDlcsImage.MaxUnauthorised)
            {
                logger.LogDebug(patchMessageFormat, newDlcsImage.Id, "maxUnauthorised", newDlcsImage.MaxUnauthorised, existingDlcsImage.MaxUnauthorised);
                patchImage ??= newDlcsImage;
            }

            // TODO - changes in ALTO, mimetype and other extras

            // need to look at all the values of this image...
            // if it doesn't require patching, return null

            // consider the "id" property and the "@id" property - some differences are not necessarily requiring a patch,
            // but if a patch is needed for something else we need to have the right values for these.
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
            imageRegistration.MaxUnauthorised = GetMaxUnauthorised(maxUnauthorised, roles);
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

        public async Task<JobActivity> GetRationalisedJobActivity(SyncOperation syncOperation, DlcsCallContext dlcsCallContext)
        {
            var batchesForImages = await GetBatchesForImages(syncOperation.ImagesExpectedOnDlcs!.Values, dlcsCallContext);
            var imageBatches = batchesForImages.ToList();
            // DASH-46
            if (syncOperation.RequiresSync == false && imageBatches.Any(b => b.Superseded == false && (b.Completed != b.Count)))
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
            var jobQuery = GetJobQuery(syncOperation.ManifestationIdentifier!); // , legacySequenceIndex: syncOperation.LegacySequenceIndex);
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
                logger.LogDebug("Image {identifier} has an error and requires reingest. Error stored in DLCS is '{error}'", 
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

        public IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string? description = null)
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
                    derivs.AddRange(dlcs.GetAVDerivatives(asset));
                }
            }
            return derivs.ToArray();
        }
    }
}