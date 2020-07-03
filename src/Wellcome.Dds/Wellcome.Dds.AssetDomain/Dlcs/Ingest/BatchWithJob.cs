using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    /// <summary>
    /// This can probably be done much better with EF...
    /// It is used to get a joined result set as fast as possible
    /// </summary>
    public class BatchWithJob
    {
        // Either
        public int Id { get; set; }

        //DlcsBatch
        public int BatchId { get; set; }
        public int DlcsIngestJobId { get; set; }
        public DateTime? RequestSent { get; set; }
        public string RequestBody { get; set; }
        public DateTime? Finished { get; set; }
        public string ResponseBody { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorText { get; set; }
        public int BatchSize { get; set; }
        public int? ContentLength { get; set; }

        // DlcsIngestJob
        public int JobId { get; set; }
        public DateTime Created { get; set; }
        public string Identifier { get; set; }
        public int SequenceIndex { get; set; }
        public string VolumePart { get; set; }
        public string IssuePart { get; set; }
        public int ImageCount { get; set; }
        public DateTime? StartProcessed { get; set; }
        public DateTime? EndProcessed { get; set; }
        public string AssetType { get; set; }
        public bool Succeeded { get; set; }
        public string Data { get; set; }
        public int ReadyImageCount { get; set; }

        public static IEnumerable<DlcsIngestJob> GetJobs(IEnumerable<BatchWithJob> batchesWithJobs)
        {
            return batchesWithJobs.GroupBy(b => b.JobId, b => b, (key, g) => GetJob(g));
        } 

        public static DlcsIngestJob GetJob(IEnumerable<BatchWithJob> batchesWithJobs)
        {
            var list = batchesWithJobs.ToList();
            if (!list.Any()) return null;
            var row1 = list[0];
            var job = new DlcsIngestJob
            {
                Id = row1.JobId,
                Created = row1.Created,
                Identifier = row1.Identifier,
                SequenceIndex = row1.SequenceIndex,
                VolumePart = row1.VolumePart,
                IssuePart = row1.IssuePart,
                ImageCount = row1.ImageCount,
                StartProcessed = row1.StartProcessed,
                EndProcessed = row1.EndProcessed,
                AssetType = row1.AssetType,
                Succeeded = row1.Succeeded,
                Data = row1.Data,
                ReadyImageCount = row1.ReadyImageCount,
                DlcsBatches = new List<DlcsBatch>()
            };
            foreach (var row in list)
            {
                var batch = new DlcsBatch
                {
                    Id = row.BatchId,
                    DlcsIngestJobId = row.DlcsIngestJobId,
                    RequestSent = row.RequestSent,
                    RequestBody = row.RequestBody,
                    Finished = row.Finished,
                    ResponseBody = row.ResponseBody,
                    ErrorCode = row.ErrorCode,
                    ErrorText = row.ErrorText,
                    BatchSize = row.BatchSize,
                    ContentLength = row.ContentLength
                };
                job.DlcsBatches.Add(batch);
            }
            return job;
        }
    }
}
