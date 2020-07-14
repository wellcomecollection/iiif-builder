﻿using System.Collections.Generic;
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
        Task ExecuteDlcsSyncOperation(SyncOperation syncOperation, bool usePriorityQueue);
        int DefaultSpace { get; set; }
        IEnumerable<DlcsIngestJob> GetMostRecentIngestJobs(string identifier, int number);
        //IEnumerable<DlcsIngestJob> GetUpdatedIngestJobs(string identifier, SyncOperation syncOperation);
        Task<Batch> GetBatch(string batchId);

        Task<JobActivity> GetRationalisedJobActivity(SyncOperation syncOperation);

        Task<IEnumerable<Batch>> GetBatchesForImages(IEnumerable<Image> images);
        Task<IEnumerable<ErrorByMetadata>> GetErrorsByMetadata();
        Task<Page<ErrorByMetadata>> GetErrorsByMetadata(int page);

        Task<int> FindSequenceIndex(string identifier);
        Task<bool> DeletePdf(string identifier);
        int RemoveOldJobs(string id);
        Task<int> DeleteOrphans(string id);
        
        IngestAction LogAction(string manifestationId, int? jobId, string userName, string action, string description = null);
        IEnumerable<IngestAction> GetRecentActions(int count, string user = null);
        Task<Dictionary<string, long>> GetDlcsQueueLevel();

        BNumberModel GetBNumberModel(string bNumber, string label);

        AVDerivative[] GetAVDerivatives(IDigitisedManifestation digitisedManifestation);
    }
}
