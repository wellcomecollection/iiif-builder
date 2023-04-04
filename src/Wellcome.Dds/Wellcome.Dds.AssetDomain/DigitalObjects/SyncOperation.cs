using System;
using System.Collections.Generic;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    /// <summary>
    /// Contains the result of a synchronisation request.
    /// Some of the tasks may have been done there and then (synchronously), others might have been queued, in batches
    /// represents the set of differences between the METS view of the world and the DLCS view
    /// </summary>
    public class SyncOperation
    {
        public Guid SyncOperationIdentifier { get; }
        
        /// <summary>
        /// The DLCS Ingest Job, if there is one, that is using this SyncOperation
        /// </summary>
        public int? JobIdentifier { get; }
        
        public string? ManifestationIdentifier { get; set; }
        public int LegacySequenceIndex { get; set; }

        // Ingest Ops
        public List<Batch> Batches { get; set; }
        public List<DlcsBatch> BatchIngestOperationInfos { get; set; }

        // Patch Ops
        public List<DlcsBatch> BatchPatchOperationInfos { get; set; } 

        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        
        /// <summary>
        /// The key is the physicalFile.StorageIdentifier
        /// The value is a Dlcs API Image.
        /// The value will be null if the image is not on the DLCS
        /// </summary>
        public Dictionary<string, Image?>? ImagesCurrentlyOnDlcs { get; set; }
        
        /// <summary>
        /// Any ingestible image that could be registered with the DLCS
        /// </summary>
        public Dictionary<string, Image?>? ImagesThatShouldBeOnDlcs { get; set; }
        public List<Image>? DlcsImagesToIngest { get; set; }
        public List<Image>? DlcsImagesToPatch { get; set; }
        public Dictionary<string, List<string>>? Mismatches { get; set; }
        public List<Image>? DlcsImagesCurrentlyIngesting { get; set; }
        public List<Image>? Orphans { get; set; }
        /// <summary>
        /// Not every file mentioned in METS needs to be synced with the DLCS.
        /// This is an optimisation so we don't have to look stuff up all the time
        /// </summary>
        public List<string>? StorageIdentifiersToIgnore { get; set; }

        public bool RequiresSync => DlcsImagesToIngest.HasItems() || DlcsImagesToPatch.HasItems();

        /// <summary>
        /// The sync operation has at least one invalid access condition and should not be synced
        /// </summary>
        public bool HasInvalidAccessCondition { get; set; }

        /// <summary>
        /// Files with no access condition in METS
        /// </summary>
        public List<IStoredFile>? MissingAccessConditions { get; set; }

        public SyncOperation(DlcsCallContext dlcsCallContext)
        {
            SyncOperationIdentifier = Guid.NewGuid();
            JobIdentifier = dlcsCallContext.JobId;
            dlcsCallContext.SyncOperationId = SyncOperationIdentifier;
            Batches = new List<Batch>();
            BatchIngestOperationInfos = new List<DlcsBatch>();
            BatchPatchOperationInfos = new List<DlcsBatch>();
        }

        public string[] GetSummary()
        {
            var summary = new List<string>
            {
                $"SyncOperationIdentifier: {SyncOperationIdentifier}",
                $"JobId: {JobIdentifier}",
                $"RequiresSync: {RequiresSync}",
                $"DlcsImagesToIngest: {DlcsImagesToIngest!.Count}",
                $"DlcsImagesToPatch: {DlcsImagesToPatch!.Count}",
                $"DlcsImagesCurrentlyIngesting: {DlcsImagesCurrentlyIngesting!.Count}",
                $"Ignored storage identifiers: {StorageIdentifiersToIgnore!.Count}",
                $"Orphans: {Orphans!.Count}",
                $"HasInvalidAccessCondition: {HasInvalidAccessCondition}",
                $"Message: {Message}"
            };
            return summary.ToArray();
        }
    }
}