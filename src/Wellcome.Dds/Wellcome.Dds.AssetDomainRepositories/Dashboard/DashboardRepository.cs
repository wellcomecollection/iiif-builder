using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using Utils;
using Wellcome.Dds.AssetDomain.AssetManagement;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.DlcsJobs;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public class DashboardRepository : IDashboardRepository
    {
        private ILogger<DashboardRepository> logger;

        private readonly IDlcs dlcs;
        private readonly IMetsRepository metsRepository;

        private static readonly string PersistentUri = StringUtils.GetAppSetting("PersistentPlayerUri", null);
        private static readonly string PersistentCatalogueRecord = StringUtils.GetAppSetting("PersistentCatalogueRecord", null);
        private static readonly string EncoreBibDataFormat = StringUtils.GetAppSetting("EncoreBibliographicData", null);
        private static readonly string OriginTemplate = StringUtils.GetAppSetting(
            "LinkedDataDomain", "https://wellcomelibrary.org").Replace("http:", "https:") + "/service/asset/{0}";
        private static readonly string ManifestTemplate = StringUtils.GetAppSetting(
            "LinkedDataDomain", "https://wellcomelibrary.org") + "/iiif/{0}/manifest";
        private static readonly int BatchSize = StringUtils.GetInt32FromAppSetting("dlcs-BatchSize", 100);
        private static readonly string ExpectedOriginBucketName = StringUtils.GetAppSetting("ArchiveStorage-ExpectedOriginBucketName", null);
        private static readonly string ExpectedStagingOriginBucketName = StringUtils.GetAppSetting("ArchiveStorage-ExpectedStagingOriginBucketName", null);
        private static readonly bool PreventSynchronisation = StringUtils.GetBoolFromAppSetting("ArchiveStorage-PreventSynchronisation", false);

        public int DefaultSpace { get; set; }

        public DashboardRepository(
            ILogger<DashboardRepository> logger,
            IDlcs dlcs, 
            IMetsRepository metsRepository)
        {
            this.dlcs = dlcs;
            DefaultSpace = dlcs.DefaultSpace;
            this.metsRepository = metsRepository;
        }

        // make all the things, then hand back to DashboarcCloudServicesJobProcessor process job.
        // the code that makes the calls to DLCS needs to go in here

        // and th sync...
        /// <summary>
        /// 
        /// </summary>
        /// <param name="identifier">Same as used for METS</param>
        /// <returns></returns>
        public IDigitisedResource GetDigitisedResource(string identifier)
        {
            IDigitisedResource digResource;
            var metsResource = metsRepository.Get(identifier);
            if (metsResource is IManifestation)
            {
                digResource = MakeDigitisedManifestation(metsResource as IManifestation);
            }
            else if (metsResource is ICollection)
            {
                digResource = MakeDigitisedCollection(metsResource as ICollection);
            }
            else
            {
                throw new ArgumentException("Cannot get a digitised resource from METS for identifier " + identifier);
            }
            digResource.Identifier = metsResource.Id;
            digResource.Partial = metsResource.Partial;
            digResource.BNumberModel = GetBNumberModel(metsResource.GetRootId(), metsResource.Label);

            //// DEBUG step - force evaluation of DLCS query
            //var list = (digResource as IDigitisedManifestation).DlcsImages.ToList();
            //foreach (var image in list)
            //{
            //    Log.DebugFormat("{0}/{1}", image.String1, image.Number2);
            //}
            return digResource;
        }

        private DigitisedCollection MakeDigitisedCollection(ICollection metsCollection)
        {
            var dc = new DigitisedCollection
            {
                MetsCollection = metsCollection,
                Identifier = metsCollection.Id
            };
            if (metsCollection.Collections.HasItems())
            {
                dc.Collections = metsCollection.Collections
                    .Select(MakeDigitisedCollection);
            }
            if (metsCollection.Manifestations.HasItems())
            {
                dc.Manifestations = metsCollection.Manifestations
                    .Select(MakeDigitisedManifestation);
            }
            return dc;
        }

        private DigitisedManifestation MakeDigitisedManifestation(IManifestation metsManifestation)
        {
            return new DigitisedManifestation
            {
                MetsManifestation = metsManifestation,
                Identifier = metsManifestation.Id,
                DlcsImages = dlcs.GetImagesForString3(metsManifestation.Id), // deferred IEnumerable
                PdfGetter = dlcs.GetPdfDetails // Func<IPdf> for deferred call
            };
        }

        public BNumberModel GetBNumberModel(string bNumber, string label)
        {
            var shortB = bNumber.Remove(8);
            return new BNumberModel
            {
                BNumber = bNumber,
                DisplayTitle = label,
                EncoreRecordUrl = string.Format(PersistentCatalogueRecord, shortB),
                ItemPageUrl = string.Format(PersistentUri.Replace("/player/", "/item/"), bNumber),
                ManifestUrl = string.Format(ManifestTemplate, bNumber),
                EncoreBiblioRecordUrl = string.Format(EncoreBibDataFormat, shortB)
            };
        }

        public void ExecuteDlcsSyncOperation(SyncOperation syncOperation, bool usePriorityQueue)
        {
            const string syncError =
                @"Configuration prevents this application from synchronising with the DLCS. Is it a staging test environment for archive storage?";
            if (PreventSynchronisation)
            {
                throw new InvalidOperationException(syncError);
            }

            logger.LogInformation("Registering BATCH INGESTS for METS resource (manifestation) with Id {0}", syncOperation.ManifestationIdentifier);
            DoBatchIngest(syncOperation.DlcsImagesToIngest, syncOperation, usePriorityQueue);

            logger.LogInformation("Registering BATCH PATCHES for METS resource (manifestation) with Id {0}", syncOperation.ManifestationIdentifier);
            DoBatchPatch(syncOperation.DlcsImagesToPatch, syncOperation);

            syncOperation.Succeeded = true;
        }

        /// <summary>
        /// Doesn't do a proper sync yet! just registers everything.
        /// 
        /// This needs to COMPARE mets data, and fix up.
        /// </summary>
        /// <param name="digitisedManifestation"></param>
        /// <param name="reIngestErrorImages"></param>
        /// <returns></returns>
        public SyncOperation GetDlcsSyncOperation(IDigitisedManifestation digitisedManifestation, bool reIngestErrorImages)
        {
            // TODO - some of this can go inside IDigitisedManifestation
            var metsManifestation = digitisedManifestation.MetsManifestation;
            var dlcsImages = digitisedManifestation.DlcsImages.ToList();
            var syncOperation = new SyncOperation
            {
                ManifestationIdentifier = metsManifestation.Id,
                ImagesAlreadyOnDlcs = GetImagesAlreadyOnDlcs(metsManifestation, dlcsImages),
                DlcsImagesCurrentlyIngesting = new List<Image>(),
                StorageIdentifiersToIgnore = metsManifestation.IgnoredStorageIdentifiers
            };

            // ImagesAlreadyOnDlcs is a map of what we think DLCS should have, to what it actually has.
            // From this we can make lists - what is missing, what is present but wrong metadata (needs patching), what is still ingesting

            // What do we need to ingest? List of assets from METS that are not present on DLCS, or are present with transcoding errors
            var assetsToIngest = new List<IPhysicalFile>();
            foreach (var kvp in syncOperation.ImagesAlreadyOnDlcs)
            {
                if (syncOperation.StorageIdentifiersToIgnore.Contains(kvp.Key))
                {
                    // We do not want to sync this image with the DLCS.
                    continue;
                }
                var image = kvp.Value;
                if (image == null || (reIngestErrorImages && HasProblemRequiringReIngest(image)))
                {
                    assetsToIngest.Add(metsManifestation.Sequence.Single(pf => pf.StorageIdentifier == kvp.Key));
                }
            }

            // Get the manifestation level metadata that each image is going to need
            syncOperation.LegacySequenceIndex = metsRepository.FindSequenceIndex(metsManifestation.Id);
            syncOperation.OriginTemplate = OriginTemplate; // GetOriginTemplate(metsManifestation.SignificantSequence[0].StorageIdentifier);
            var ddsId = new DdsIdentifier(metsManifestation.Id);

            // This sets the default maxUnauthorised, before we know what the roles are. 
            // This is the default maxUnauthorised for the manifestation based only on on permittedOperations.
            // later we might override this for individual images.
            int maxUnauthorised = 1000;
            if (metsManifestation.PermittedOperations.Contains("wholeImageHighResAsJpg"))
            {
                maxUnauthorised = -1; // Use DLCS default max size
            }

            // What do we need to patch? List of existing DLCS images that don't have the correct metadata
            syncOperation.DlcsImagesToIngest = new List<Image>();
            syncOperation.DlcsImagesToPatch = new List<Image>();
            syncOperation.Orphans = dlcsImages.Where(image => ! syncOperation.ImagesAlreadyOnDlcs.ContainsKey(image.StorageIdentifier)).ToList();

            foreach (var physicalFile in metsManifestation.Sequence)
            {
                if (syncOperation.StorageIdentifiersToIgnore.Contains(physicalFile.StorageIdentifier))
                {
                    // We do not want to sync this image with the DLCS.
                    continue;
                }
                var newDlcsImage = MakeDlcsImage(physicalFile, syncOperation.OriginTemplate, ddsId, syncOperation.LegacySequenceIndex, maxUnauthorised);
                var existingDlcsImage = syncOperation.ImagesAlreadyOnDlcs[physicalFile.StorageIdentifier];

                if (assetsToIngest.Contains(physicalFile))
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
            return syncOperation;
        }
        

        /// <summary>
        /// Images that match the metadata but are not in the METS
        /// </summary>
        /// <param name="imagesAlreadyOnDlcs"></param>
        /// <param name="dlcsImages"></param>
        /// <returns></returns>
        private List<Image> GetOrphans(Dictionary<string, Image> imagesAlreadyOnDlcs, IEnumerable<Image> dlcsImages)
        {
            return dlcsImages.Where(image => !imagesAlreadyOnDlcs.ContainsKey(image.StorageIdentifier)).ToList();
        }

        public Batch GetBatch(string batchId)
        {
            var batchOp = dlcs.GetBatch(batchId);

            return batchOp.ResponseObject;
        }
        
        private Dictionary<string, Image> GetImagesAlreadyOnDlcs(
            IManifestation metsManifestation, List<Image> dlcsImages)
        {
            // create an empty dictionary for all the images we need to have in the DLCS:
            var imagesAlreadyOnDlcs = new Dictionary<string, Image>();
            foreach (var physicalFile in metsManifestation.Sequence)
            {
                imagesAlreadyOnDlcs[physicalFile.StorageIdentifier] = null;
            }

            // go through all the DLCS images
            PopulateImagesAlreadyOnDlcs(imagesAlreadyOnDlcs, metsManifestation, dlcsImages);

            // do we have any local GUIDs that the DLCS doesn't have? If metadata has changed, our initial query might miss them.
            // so we should fetch by IDs
            var missingDlcsImageIds = imagesAlreadyOnDlcs
                .Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
            if (missingDlcsImageIds.Any())
            {
                // See if the DLCS has these IDs anyway, in the same space but maybe with different metadata
                var mismatchedImages = dlcs.GetImagesByDlcsIdentifiers(missingDlcsImageIds);
                PopulateImagesAlreadyOnDlcs(imagesAlreadyOnDlcs, metsManifestation, mismatchedImages);
            }
            return imagesAlreadyOnDlcs;
        }

        public IEnumerable<Batch> GetBatchesForImages(IEnumerable<Image> images)
        {
            List<string> batchIds = new List<string>();
            foreach (var image in images)
            {
                if (image != null && image.Batch.HasText() && !batchIds.Contains(image.Batch))
                {
                    batchIds.Add(image.Batch);
                }
            }
            // DLCS image.batch is just an ID, not a URI; these needs to be fixed to improve this code...
            return batchIds
                .Select(GetBatch)
                .Where(batch => batch != null);
        }

        private void DoBatchPatch(List<Image> dlcsImagesToPatch, SyncOperation syncOperation)
        {
            // TODO - refactor this and DoBatchIngest - They use a different kind of Operation
            foreach (var batch in dlcsImagesToPatch.Batch(BatchSize))
            {
                var imagePatches = batch.ToArray();
                logger.LogInformation("Batch of {0}", imagePatches.Length);

                if (imagePatches.Length == 0)
                {
                    logger.LogInformation("zero length - abandoning.");
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
                var registrationOperation = dlcs.PatchImages(imageRegistrationsAsHydraCollection);
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

        private void DoBatchIngest(List<Image> dlcsImagesToIngest, SyncOperation syncOperation, bool priority)
        {
            foreach (var batch in dlcsImagesToIngest.Batch(BatchSize))
            {
                var imageRegistrations = batch.ToArray();
                logger.LogInformation("Batch of {0}", imageRegistrations.Length);

                if (imageRegistrations.Length == 0)
                {
                    logger.LogInformation("zero length - abandoning.");
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
                var registrationOperation = dlcs.RegisterImages(imageRegistrationsAsHydraCollection, priority);
                dbDlcsBatch.Finished = DateTime.Now;
                dbDlcsBatch.RequestBody = registrationOperation.RequestJson;
                dbDlcsBatch.ResponseBody = registrationOperation.ResponseJson;
                if (registrationOperation.Error != null)
                {
                    dbDlcsBatch.ErrorCode = registrationOperation.Error.Status;
                    dbDlcsBatch.ErrorText = registrationOperation.Error.Message;
                }
                syncOperation.BatchIngestOperationInfos.Add(dbDlcsBatch);
                syncOperation.Batches.Add(registrationOperation.ResponseObject);
            }
        }

        /// <summary>
        /// return newDlcsImage if it differes from existingDlcsImage in a way that requires patching
        /// </summary>
        /// <param name="newDlcsImage"></param>
        /// <param name="existingDlcsImage"></param>
        /// <returns></returns>
        private Image GetPatchImage(Image newDlcsImage, Image existingDlcsImage)
        {
            // TODO: ?
            //if (existingDlcsImage.ImageOptimisationPolicy != newDlcsImage.ImageOptimisationPolicy)
            //    return newDlcsImage;
            //if (existingDlcsImage.ThumbnailPolicy != newDlcsImage.ThumbnailPolicy)
            //    return newDlcsImage;

            if (existingDlcsImage.Origin != newDlcsImage.Origin)
            {
                return newDlcsImage;
                // don't mess about with matching bucket names, now that the paths are versioned.
                // we need to know about any difference.
                // This still might be OK if the mismatch is bucket name only
                //if (!ExpectedStagingOriginBucketName.HasText())
                //{
                //    throw new ArgumentNullException("ExpectedStagingOriginBucketName", "Must be supplied");
                //}
                //var possibleProductionOrigin = newDlcsImage.Origin.Replace(ExpectedStagingOriginBucketName,
                //    ExpectedOriginBucketName);
                //if (existingDlcsImage.Origin != possibleProductionOrigin)
                //{
                //    // This really does require patching.
                //    return newDlcsImage;
                //}
            }
            if (existingDlcsImage.String1 != newDlcsImage.String1)
                return newDlcsImage;
            if (existingDlcsImage.String2 != newDlcsImage.String2)
                return newDlcsImage;
            if (existingDlcsImage.String3 != newDlcsImage.String3)
                return newDlcsImage;
            if (existingDlcsImage.Number1 != newDlcsImage.Number1)
                return newDlcsImage;
            if (existingDlcsImage.Number2 != newDlcsImage.Number2)
                return newDlcsImage;
            if (existingDlcsImage.Number3 != newDlcsImage.Number3)
                return newDlcsImage;
            if (existingDlcsImage.MediaType != newDlcsImage.MediaType)
                return newDlcsImage;


            // Do we care about ordering?
            if (!AreEqual(existingDlcsImage.Tags, newDlcsImage.Tags))
                return newDlcsImage;
            if (!AreEqual(existingDlcsImage.Roles, newDlcsImage.Roles))
                return newDlcsImage;
            if (existingDlcsImage.MaxUnauthorised != newDlcsImage.MaxUnauthorised)
                return newDlcsImage;

            // TODO - changes in ALTO, mimetype and other extras
            

            // need to look at all the values of this image...
            // if it doesn't require patching, return null

            // consider the "id" property and the "@id" property - some differences are not necessarily requiring a patch,
            // but if a patch is needed for something else we need to have the right values for these.
            return null;
        }

        private bool AreEqual(string[] s1, string[] s2)
        {
            if (s1 == null)
            {
                return s2 == null || s2.Length == 0;
            }
            if (s2 == null)
            {
                return s1.Length == 0;
            }
            return s1.SequenceEqual(s2);
        }

        private Image MakeDlcsImage(
            IPhysicalFile asset, 
            string originTemplate, 
            DdsIdentifier ddsId, 
            int sequenceIndex,
            int maxUnauthorised)
        {
            string origin;
            if (originTemplate.HasText())
            {
                origin = string.Format(originTemplate, asset.StorageIdentifier);
            }
            else
            {
                origin = asset.GetStoredFileInfo().Uri;
            }
            var imageRegistration = new Image
            {
                StorageIdentifier = asset.StorageIdentifier,
                ModelId = asset.StorageIdentifier, // will be patched to full path later
                Space = dlcs.DefaultSpace,
                Origin = origin,
                String1 = ddsId.BNumber, // will be string reference
                Number1 = sequenceIndex,
                Number2 = asset.Index,
                MediaType = asset.MimeType,
                Family = (char)asset.Family
            };
            if (asset.RelativeAltoPath.HasText())
            {
                // TODO - Give the URI from where the DLCS can fetch this. Use the proper identifier not the seqIndex.
                imageRegistration.Text = asset.RelativeAltoPath;
                imageRegistration.TextType = "alto"; // also need a string to identify this as ALTO
            }
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                    imageRegistration.String2 = ddsId.BNumber;
                    imageRegistration.String3 = ddsId.BNumber;
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
            var roles = GetRoles(asset);
            imageRegistration.Roles = roles;
            imageRegistration.MaxUnauthorised = GetMaxUnauthorised(maxUnauthorised, roles);
            return imageRegistration;
        }

        public IEnumerable<DlcsIngestJob> GetMostRecentIngestJobs(string identifier, int number)
        {
            int sequenceIndex = metsRepository.FindSequenceIndex(identifier);

            using (var ctx = new CloudIngestContext())
            {
                var jobQuery = GetJobQuery(identifier, ctx, legacySequenceIndex: sequenceIndex);
                if (jobQuery == null)
                {
                    return new DlcsIngestJob[0];
                }
                return jobQuery
                    .Include(j => j.DlcsBatches)
                    .OrderByDescending(j => j.Created)
                    .Take(number)
                    .ToList();
            }
        }

        public JobActivity GetRationalisedJobActivity(SyncOperation syncOperation)
        {
            var imageBatches = GetBatchesForImages(syncOperation.ImagesAlreadyOnDlcs.Values).ToList();
            // DASH-46
            if (syncOperation.RequiresSync == false && imageBatches.Any(b => b.Superseded == false && (b.Completed != b.Count)))
            {
                // Some of these batches may seem incomplete, but they have been superseded
                imageBatches = dlcs.GetTestedImageBatches(imageBatches);
            }
            var updatedJobs = GetUpdatedIngestJobs(syncOperation, imageBatches).ToList();
            return new JobActivity {BatchesForCurrentImages = imageBatches, UpdatedJobs = updatedJobs};
        }

        public int RemoveOldJobs(string id)
        {
            int sequenceIndex = metsRepository.FindSequenceIndex(id);

            using (var ctx = new CloudIngestContext())
            {
                var jobQuery = GetJobQuery(id, ctx, legacySequenceIndex: sequenceIndex);
                if (jobQuery == null)
                {
                    return 0;
                }
                var jobs = jobQuery.OrderByDescending(j => j.Created).Skip(1).ToList();
                int num = jobs.Count;
                if (num > 0)
                {
                    foreach (var jobToRemove in jobs)
                    {
                        ctx.DlcsIngestJobs.Remove(jobToRemove);
                    }
                    ctx.SaveChanges();
                }
                return num;
            }
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
            using (var ctx = new CloudIngestContext())
            {
                var jobQuery = GetJobQuery(syncOperation.ManifestationIdentifier, ctx, legacySequenceIndex: syncOperation.LegacySequenceIndex);
                if (jobQuery == null)
                {
                    return new DlcsIngestJob[0];
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
                        ctx.SaveChanges();
                    }
                }
                return jobs;
            }
        }

        private IQueryable<DlcsIngestJob> GetJobQuery(string identifier, CloudIngestContext ctx, int legacySequenceIndex = -1)
        {
            var ddsId = new DdsIdentifier(identifier);            
            IQueryable<DlcsIngestJob> jobQuery = null;
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                    jobQuery = ctx.DlcsIngestJobs.Where(j => j.Identifier == identifier);
                    break;
                case IdentifierType.Volume:
                    //int sequenceIndex = metsRepository.FindSequenceIndex(identifier);
                    jobQuery = ctx.DlcsIngestJobs.Where(j => (j.VolumePart != null && j.VolumePart == identifier)
                    || (j.VolumePart == null && j.Identifier == ddsId.BNumber && j.SequenceIndex == legacySequenceIndex));
                    break;
                case IdentifierType.BNumberAndSequenceIndex:
                    jobQuery =
                        ctx.DlcsIngestJobs.Where(j => j.Identifier == ddsId.BNumber && j.SequenceIndex == ddsId.SequenceIndex);
                    break;
                case IdentifierType.Issue:
                    jobQuery = ctx.DlcsIngestJobs.Where(j => j.IssuePart == identifier);
                    break;
            }
            return jobQuery;
        }


        private bool HasProblemRequiringReIngest(Image dlcsImage)
        {
            bool error = dlcsImage.Error.HasText();
            return error;
        }

        private void PopulateImagesAlreadyOnDlcs(
            Dictionary<string, Image> imageDictionary, 
            IManifestation thisManifestation,
            IEnumerable<Image> imagesOnDlcs)
        {
            foreach (var dlcsImage in imagesOnDlcs)
            {

                var physFile = thisManifestation.Sequence.SingleOrDefault(pf => pf.StorageIdentifier == dlcsImage.StorageIdentifier);
                if (physFile != null)
                {
                    // this DLCS image belongs in the dictionary
                    imageDictionary[dlcsImage.StorageIdentifier] = dlcsImage;
                }
            }
        }

        private static readonly char[] SlashSeparator = new [] {'/'};

        private Guid GetLocalGuid(string id)
        {
            // This relies on the fact that all Preservica / Wellcome IDs have a GUID as their last component
            var last = id.Split(SlashSeparator).Last();
            return new Guid(last);
        }


        private string[] GetRoles(IPhysicalFile asset)
        {
            if (asset.AccessCondition == AccessCondition.Open)
            {
                return new string[0];
            }
            var acUri = dlcs.GetRoleUri(asset.AccessCondition);
            logger.LogInformation("Asset will be registered with role {0}", acUri);
            return new[] { acUri };
        }

        private string reqRegUri;
        private int GetMaxUnauthorised(int sequenceMaxSize, string[] roles)
        {
            if (roles.HasItems())
            {
                if (reqRegUri == null)
                {
                    reqRegUri = dlcs.GetRoleUri(AccessCondition.RequiresRegistration);
                }
                if (roles.Contains(reqRegUri))
                {
                    return 200;
                }
                return 0;
            }
            return sequenceMaxSize;
        }

        public IEnumerable<ErrorByMetadata> GetErrorsByMetadata()
        {
            return dlcs.GetErrorsByMetadata();
        }

        public Page<ErrorByMetadata> GetErrorsByMetadata(int page)
        {
            return dlcs.GetErrorsByMetadata(page);
        }

        /// <summary>
        /// This could be removed once alto search is replaced.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public int FindSequenceIndex(string identifier)
        {
            return metsRepository.FindSequenceIndex(identifier);
        }

        public bool DeletePdf(string string1, int number1)
        {
            return dlcs.DeletePdf(string1, number1);
        }

        public int DeleteOrphans(string id)
        {
            var manif = (IDigitisedManifestation)GetDigitisedResource(id);
            var syncOp = GetDlcsSyncOperation(manif, false);
            return dlcs.DeleteImages(syncOp.Orphans);
        }

        public IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string description = null)
        {
            using (var ctx = new CloudIngestContext())
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
                ctx.IngestActions.Add(ia);
                ctx.SaveChanges();
                return ia;
            }
        }

        public IEnumerable<IngestAction> GetRecentActions(int count, string user = null)
        {
            using (var ctx = new CloudIngestContext())
            {
                IQueryable<IngestAction> q = ctx.IngestActions.OrderByDescending(ia => ia.Id);
                if (user.HasText())
                {
                    q = q.Where(ia => ia.Username == user);
                }
                return q.Take(count).ToList();
            }
        }

        public Dictionary<string, long> GetDlcsQueueLevel()
        {
            return dlcs.GetDlcsQueueLevel();
        }
    }
}
