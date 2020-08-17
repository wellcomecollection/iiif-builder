using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Utils;
using Utils.Logging;

namespace Wellcome.Dds.Dashboard.Models
{
    public class MigrationModel
    {
        public string Identifier { get; set; }
        public string StorageManifestMessage { get; set; }
        public bool StorageManifestFailed { get; set; }
        public DateTime? StorageManifestDate { get; set; }
        public string StorageManifestDescription { get; set; }

        public string MigrationBucket { get; set; }
        public DateTime? BagDate { get; set; }
        public string BagDescription { get; set; }
        public string BagSize { get; set; }
        public string BagName { get; set; }

        public BaggingError BaggingError { get; set; }
        public string BaggingErrorDescription { get; set; }

        public List<IngestRecord> LocalIngests { get; set; }
        public List<IngestRecord> KnownIngests { get; set; }
        public List<IngestRecord> UnionIngests { get; set; }
        public Ingest LatestIngest { get; set; }
        public List<LoggingEvent> Log { get; set; }

        public MigrationStatus MigrationStatus { get; set; }
    }


    public class IngestEvent
    {
        public DateTime Created { get; set; }
        public string Description { get; set; }
    }

    public class Ingest
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public List<IngestEvent> Events { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }

        public static Ingest FromJObject(JObject jIngest)
        {
            var sourceLoc = jIngest["sourceLocation"];
            var ingest = new Ingest
            {
                Id = jIngest["id"].Value<string>(),
                Source = sourceLoc["bucket"].Value<string>() + "/" + sourceLoc["path"].Value<string>(),
                Status = jIngest["status"]["id"].Value<string>(),
                Events = jIngest["events"].Select(je => new IngestEvent
                {
                    Created = je["createdDate"].Value<DateTime>(),
                    Description = je["description"].Value<string>()
                }).ToList(),
                Message = "(No events)"
            };
            if (ingest.Events.Any())
            {
                ingest.Message =  "created " + StringUtils.GetFriendlyAge(ingest.Events[0].Created);
            }
            return ingest;
        }
    }

    [Serializable]
    public class IngestRecord
    {
        public string IngestId { get; set; }
        public string Identifier { get; set; }
        public DateTime Created { get; set; }
        public string Type { get; set; }
    }

    public class MigrationResult
    {
        public string Message { get; set; }
        public bool Success { get; set; }
        public string Id { get; set; }
    }

    public class DdsCallResult
    {
        public string Message { get; set; }
        public bool Success { get; set; }
    }

    public class MigrationBagInfo
    {
        public string Bucket { get; set; }
        public string Key { get; set; }
        public bool Exists { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
    }

    public class BaggingError
    {
        public string Identifier { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime Created { get; set; }
        public string Error { get; set; }
    }

    public class MigrationReport
    {
        public DateTime Modified { get; set; }
        public JToken RawReport { get; set; }

        public MigrationBatch GlobalBatch { get; set; }
        public List<MigrationBatch> IngestBatches { get; set; }
        public List<MigrationBatch> BaggingBatches { get; set; }
        public List<MigrationBatch> AllBatches { get; set; }
    }

    public class MigrationBatch
    {
        public string Id { get; set; }
        public string BatchType { get; set; }
        public string Filter { get; set; }
        public int Count { get; set; }
        // public ReportPie Pie { get; set; }
        public int RequireUpdate { get; set; }
        public DateTime? EarliestUpdate { get; set; }
        public DateTime? LatestUpdate { get; set; }
        public DateTime? EarliestBatchDate { get; set; }
        public DateTime? LatestBatchDate { get; set; }
        public BaggingError[] BaggingErrors { get; set; }
        public BaggingError[] IngestNotSucceeded { get; set; }
        public ReportPie BaggingPie { get; set; }
        public ReportPie IngestPie { get; set; }
        public ReportPie PackagePie { get; set; }
        public ReportPie TextPie { get; set; }
        public ReportPie DlcsPie { get; set; }
    }

    public class ReportPie
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public decimal SucceededPercent { get; set; }
        public int KnownErrors { get; set; }
        public decimal KnownErrorsPercent { get; set; }
        public int Unknown { get; set; }
        public decimal UnknownPercent { get; set; }
    }

    /// <summary>
    /// A row from the DynamoDB table
    /// </summary>
    public class MigrationStatus
    {
        public string Identifier { get; set; }
        public DateTime? Updated { get; set; }
        public DateTime? BaggerStart { get; set; }
        public DateTime? BaggerEnd { get; set; }
        public DateTime? BagDate { get; set; }
        public long BagSize { get; set; }
        public string IngestId { get; set; }
        public DateTime? IngestDate { get; set; }
        public DateTime? IngestStart { get; set; }
        public string IngestStatus { get; set; }
        public string BaggingError { get; set; }
        public DateTime? PackageDate { get; set; }
        public DateTime? DdsCallReceived { get; set; }
    }
}