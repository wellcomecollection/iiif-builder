using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDashboardRepository
    {
        IDigitisedResource GetDigitisedResource(string identifier);
        SyncOperation GetDlcsSyncOperation(IDigitisedManifestation digitisedManifestation, bool reIngestErrorImages);
        void ExecuteDlcsSyncOperation(SyncOperation syncOperation, bool usePriorityQueue);
        int DefaultSpace { get; set; }
        IEnumerable<DlcsIngestJob> GetMostRecentIngestJobs(string identifier, int number);
        //IEnumerable<DlcsIngestJob> GetUpdatedIngestJobs(string identifier, SyncOperation syncOperation);
        Batch GetBatch(string batchId);

        JobActivity GetRationalisedJobActivity(SyncOperation syncOperation);

        IEnumerable<Batch> GetBatchesForImages(IEnumerable<Image> images);
        IEnumerable<ErrorByMetadata> GetErrorsByMetadata();
        Page<ErrorByMetadata> GetErrorsByMetadata(int page);

        int FindSequenceIndex(string identifier);
        bool DeletePdf(string string1, int number1);
        int RemoveOldJobs(string id);
        int DeleteOrphans(string id);
        
        IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string description = null);
        IEnumerable<IngestAction> GetRecentActions(int count, string user = null);
        Dictionary<string, long> GetDlcsQueueLevel();

        BNumberModel GetBNumberModel(string bNumber, string label);
    }
}
