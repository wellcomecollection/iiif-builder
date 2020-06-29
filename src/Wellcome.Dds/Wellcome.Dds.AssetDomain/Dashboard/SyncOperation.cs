using System.Collections.Generic;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    /// <summary>
    /// Contains the result of a synchronisation request.
    /// Some of the tasks may have been done there and then (synchronously), others might have been queued, in batches
    /// represents the set of differences between the METS view of the world and the DLCS view
    /// </summary>
    public class SyncOperation
    {
        public string ManifestationIdentifier { get; set; }
        public int LegacySequenceIndex { get; set; }

        // Ingest Ops
        public List<Batch> Batches { get; set; }
        public List<DlcsBatch> BatchIngestOperationInfos { get; set; }

        // Patch Ops
        public List<DlcsBatch> BatchPatchOperationInfos { get; set; } 

        public bool Succeeded { get; set; }
        public string Message { get; set; }
        /// <summary>
        /// The key is the physicalFile.StorageIdentifier
        /// The value is a Dlcs API Image.
        /// The value will be null if the image is not on the DLCS
        /// </summary>
        public Dictionary<string, Image> ImagesAlreadyOnDlcs { get; set; }
        public List<Image> DlcsImagesToIngest { get; set; }
        public List<Image> DlcsImagesToPatch { get; set; }
        public List<Image> DlcsImagesCurrentlyIngesting { get; set; }
        public List<Image> Orphans { get; set; }
        /// <summary>
        /// Not every PhysicalFile needs to be synced with the DLCS
        /// </summary>
        public List<string> StorageIdentifiersToIgnore { get; set; }
        public string OriginTemplate { get; set; }

        public bool RequiresSync
        {
            get { return DlcsImagesToIngest.HasItems() || DlcsImagesToPatch.HasItems(); }
        }

        public SyncOperation()
        {
            Batches = new List<Batch>();
            BatchIngestOperationInfos = new List<DlcsBatch>();
            BatchPatchOperationInfos = new List<DlcsBatch>();
        }
    }
}