using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Utils.Database;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories.Control;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories
{
    /// <summary>
    /// This class merges CloudIngestContext and WorkflowContext from old DDS,
    /// which both use the Instrumentation database.
    /// 
    /// Two other DbContext classes are present in old Dds:
    /// 
    /// TranscodeContext - for processing AV jobs before DLCS
    /// DdsInstrumentationContext - for the old DDS Dashboard
    /// 
    /// The first of these is definitely obsolete, the second SHOULD be.
    /// </summary>
    public class DdsInstrumentationContext : DbContext
    {
        public DdsInstrumentationContext(DbContextOptions<DdsInstrumentationContext> options) : base(options)
        { }

        // From CloudIngestContext:
        public DbSet<DlcsIngestJob> DlcsIngestJobs => Set<DlcsIngestJob>();
        public DbSet<DlcsBatch> DlcsBatches => Set<DlcsBatch>();
        public DbSet<IngestAction> IngestActions => Set<IngestAction>();

        // from WorkflowContext:
        public DbSet<WorkflowJob> WorkflowJobs => Set<WorkflowJob>();

        public DbSet<ControlFlow> ControlFlows => Set<ControlFlow>();

        public async Task<int> CountBatchesAsync()
        {
            return await DlcsBatches.CountAsync();
        }

        public int ClearValidBatches(int from, int to)
        {
            var sql = "UPDATE dlcs_batches SET request_body=null, response_body=null"
                + $" WHERE id > {from} AND id <= {to} AND finished IS NOT NULL"
                + " AND error_code = 0 AND error_text IS null";
            return Database.ExecuteSqlRaw(sql);
        }

        public async Task<WorkflowJob> PutJob(DdsIdentifier ddsId, bool forceRebuild, bool take, int? workflowOptions,
            bool expedite, bool flushCache)
        {
            WorkflowJob? job = await WorkflowJobs.FindAsync(ddsId.PackageIdentifier);
            if (job == null)
            {
                job = new WorkflowJob {Identifier = ddsId.PackageIdentifier};
                await WorkflowJobs.AddAsync(job);
            }

            job.Created = DateTime.UtcNow;
            job.WorkflowOptions = workflowOptions >= 0 ? workflowOptions : null;
            if (take)
            {
                job.Waiting = false;
                job.Taken = DateTime.UtcNow;
            }
            else
            {
                job.Taken = null;
                job.Waiting = true;
            }

            job.Finished = false;
            job.Error = null;
            job.ForceTextRebuild = forceRebuild;
            job.FlushCache = flushCache;
            job.Expedite = expedite;
            await SaveChangesAsync();
            return job;
        }

        public string? MarkFirstJobAsTaken(int minAgeInMinutes)
        {
            var sql = "update workflow_jobs set waiting=false, taken=now() where identifier = ( "
                + " select identifier from workflow_jobs "
                + " where waiting=true "
                + $" and (created < now() - interval '{minAgeInMinutes} minutes' or expedite=true) "
                + " and ingest_job_started is null "
                + " order by expedite desc, created "
                + " limit 1 "
                + " for update skip locked "
                + ") returning identifier;";
            return Database.MapRawSql(sql, MapString).FirstOrDefault();
        }

        private string? MapString(DbDataReader dr)
        {
            if (dr.IsDBNull(0)) return null;
            return (string) dr[0];
        }

        public int FinishAllJobs()
        {
            const string sql = "update workflow_jobs set waiting=false, ingest_job_started=null, finished=true, workflow_options=null, " +
                               "error='Force-finished before job could be taken' where waiting=true";
            return Database.ExecuteSqlRaw(sql);
        }

        public int ResetJobsMatchingError(string error)
        {
            const string sql = "update workflow_jobs set waiting=true, ingest_job_started=null, finished=false, taken=null, " +
                               "error=null, workflow_options=null where error like '%' || {0} || '%'";
            return Database.ExecuteSqlRaw(sql, error);
        }

        public async Task<List<WorkflowJob>> GetJobsRegisteringImages(int numberToTake, CancellationToken cancellationToken)
        {
            var jobs = await WorkflowJobs
                .Where(job => job.IngestJobStarted != null)
                .OrderBy(job => job.Taken) // ascending, oldest first
                .Take(numberToTake)
                .ToListAsync(cancellationToken);
            return jobs;
        }
    }

    [Serializable]
    public class DdsInstrumentationDbException : Exception
    {
        public DdsInstrumentationDbException()
            : base() { }

        public DdsInstrumentationDbException(string message)
            : base(message) { }

        public DdsInstrumentationDbException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public DdsInstrumentationDbException(string message, Exception innerException)
            : base(message, innerException) { }

        public DdsInstrumentationDbException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected DdsInstrumentationDbException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}