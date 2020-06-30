using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.AssetDomainRepositories.WorkflowJobs
{
    public class WorkflowContext : DbContext
    {
        public DbSet<WorkflowJob> WorkflowJobs { get; set; }

        public static int CountBatches()
        {
            throw new NotImplementedException("SQL Dep..");
            //using (var ctx = new WorkflowContext())
            //{
            //    var batchCount = ctx.Database.SqlQuery<int>("SELECT COUNT(*) FROM DlcsBatches");
            //    return batchCount.First();
            //}
        }


        public static int ClearValidBatches(int from, int to)
        {
            throw new NotImplementedException("SQL Dep..");
 //           const string template =
 //               @"update DlcsBatches set RequestBody = null, ResponseBody = null where
 //Id > $from and Id <= $to and Finished is not null and ErrorCode = 0 and ErrorText is null";

 //           var sql = template.Replace("$from", from.ToString()).Replace("$to", to.ToString());
 //           using (var ctx = new WorkflowContext())
 //           {
 //               var rows = ctx.Database.ExecuteSqlCommand(sql);
 //               return rows;
 //           }
        }

        public static WorkflowJob PutJob(string bNumber, bool forceRebuild, bool take)
        {
            throw new NotImplementedException("Create");
            //    WorkflowJob job;
            //    using (var ctx = new WorkflowContext())
            //    {
            //        job = ctx.WorkflowJobs.Find(bNumber);
            //        if (job == null)
            //        {
            //            job = ctx.WorkflowJobs.Create();
            //            job.Identifier = bNumber;
            //            ctx.WorkflowJobs.Attach(job);
            //            ctx.Entry(job).State = EntityState.Added;
            //        }
            //        job.Created = DateTime.Now;
            //        if (take)
            //        {
            //            job.Waiting = false;
            //            job.Taken = DateTime.Now;
            //        }
            //        else
            //        {
            //            job.Taken = null;
            //            job.Waiting = true;
            //        }
            //        job.Finished = false;
            //        job.Error = null;
            //        job.ForceTextRebuild = forceRebuild;
            //        ctx.SaveChanges();
            //    }
            //    return job;
            //}
        }
    }
}
