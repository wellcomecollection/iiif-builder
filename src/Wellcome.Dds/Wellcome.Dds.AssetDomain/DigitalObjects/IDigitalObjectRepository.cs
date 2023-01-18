using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IDigitalObjectRepository
    {
        Task<IDigitalObject> GetDigitalObject(DdsIdentifier identifier, DlcsCallContext dlcsCallContext, bool includePdfDetails = false);
        Task<SyncOperation> GetDlcsSyncOperation(
            IDigitalManifestation digitisedManifestation,
            bool reIngestErrorImages,
            DlcsCallContext dlcsCallContext);
        
        Task ExecuteDlcsSyncOperation(SyncOperation syncOperation, bool usePriorityQueue, DlcsCallContext dlcsCallContext);
        int DefaultSpace { get; }
        Task<IEnumerable<DlcsIngestJob>> GetMostRecentIngestJobs(string identifier, int number);
        //IEnumerable<DlcsIngestJob> GetUpdatedIngestJobs(string identifier, SyncOperation syncOperation);
        Task<Batch?> GetBatch(string batchId, DlcsCallContext dlcsCallContext);

        Task<JobActivity> GetRationalisedJobActivity(SyncOperation syncOperation, DlcsCallContext dlcsCallContext);

        Task<IEnumerable<Batch>> GetBatchesForImages(IEnumerable<Image> images, DlcsCallContext dlcsCallContext);
        Task<IEnumerable<ErrorByMetadata>> GetErrorsByMetadata(DlcsCallContext dlcsCallContext);
        Task<Page<ErrorByMetadata>> GetErrorsByMetadata(int page, DlcsCallContext dlcsCallContext);

        Task<int> FindSequenceIndex(string identifier);
        Task<bool> DeletePdf(string identifier);
        Task<int> RemoveOldJobs(string id);
        Task<int> DeleteOrphans(string id, DlcsCallContext dlcsCallContext);
        
        IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string? description = null);
        IEnumerable<IngestAction> GetRecentActions(int count, string? user = null);
        Task<Dictionary<string, long>> GetDlcsQueueLevel();
        AVDerivative[] GetAVDerivatives(IDigitalManifestation digitisedManifestation);
    }
}
