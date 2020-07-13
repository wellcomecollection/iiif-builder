using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDashboardRepository
    {
        Task<IDigitisedResource> GetDigitisedResourceAsync(string identifier);
        Task<SyncOperation> GetDlcsSyncOperation(IDigitisedManifestation digitisedManifestation,
            bool reIngestErrorImages);
        void ExecuteDlcsSyncOperation(SyncOperation syncOperation, bool usePriorityQueue);
        int DefaultSpace { get; set; }
        Task<IEnumerable<DlcsIngestJob>> GetMostRecentIngestJobs(string identifier, int number);
        //IEnumerable<DlcsIngestJob> GetUpdatedIngestJobs(string identifier, SyncOperation syncOperation);
        Batch GetBatch(string batchId);

        JobActivity GetRationalisedJobActivity(SyncOperation syncOperation);

        IEnumerable<Batch> GetBatchesForImages(IEnumerable<Image> images);
        IEnumerable<ErrorByMetadata> GetErrorsByMetadata();
        Page<ErrorByMetadata> GetErrorsByMetadata(int page);

        Task<int> FindSequenceIndex(string identifier);
        bool DeletePdf(string string1, int number1);
        Task<int> RemoveOldJobs(string id);
        Task<int> DeleteOrphans(string id);
        
        IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string description = null);
        IEnumerable<IngestAction> GetRecentActions(int count, string user = null);
        Dictionary<string, long> GetDlcsQueueLevel();

        BNumberModel GetBNumberModel(string bNumber, string label);

        AVDerivative[] GetAVDerivatives(IDigitisedManifestation digitisedManifestation);
    }
}
