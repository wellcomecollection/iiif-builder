using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.AssetDomainRepositories
{
    /// <summary>
    /// This class merges WorlflowContext and WorkflowContext from old DDS,
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
        public DbSet<DlcsIngestJob> DlcsIngestJobs { get; set; }
        public DbSet<DlcsBatch> DlcsBatches { get; set; }
        public DbSet<IngestAction> IngestActions { get; set; }

        // from WorkflowContext:
        public DbSet<WorkflowJob> WorkflowJobs { get; set; }

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

        public async Task<WorkflowJob> PutJob(string bNumber, bool forceRebuild, bool take, int? workflowOptions)
        {
            WorkflowJob job = await WorkflowJobs.FindAsync(bNumber);
            if (job == null)
            {
                job = new WorkflowJob {Identifier = bNumber};
                await WorkflowJobs.AddAsync(job);
            }

            job.Created = DateTime.Now;
            if (workflowOptions >= 0)
            {
                job.WorkflowOptions = workflowOptions;
            }
            if (take)
            {
                job.Waiting = false;
                job.Taken = DateTime.Now;
            }
            else
            {
                job.Taken = null;
                job.Waiting = true;
            }

            job.Finished = false;
            job.Error = null;
            job.ForceTextRebuild = forceRebuild;
            await SaveChangesAsync();
            return job;
        }

        public int FinishAllJobs()
        {
            var sql = "update workflow_jobs set waiting=false, finished=true, error='Force-finished before job could be taken' where waiting=true";
            return Database.ExecuteSqlRaw(sql);
        }
    }
}