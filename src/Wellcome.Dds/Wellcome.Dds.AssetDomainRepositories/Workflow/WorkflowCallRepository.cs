using System;
using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.AssetDomain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Wellcome.Dds.AssetDomainRepositories.Workflow
{
    public class WorkflowCallRepository : IWorkflowCallRepository
    {
        private DdsInstrumentationContext ddsInstrumentationContext;

        public WorkflowCallRepository(DdsInstrumentationContext ddsInstrumentationContext)
        {
            this.ddsInstrumentationContext = ddsInstrumentationContext;
        }

        private const int RecentSampleHours = 2;

        public List<WorkflowJob> GetRecent()
        {
            return ddsInstrumentationContext.WorkflowJobs
                .OrderByDescending(j => j.Created)
                .Take(1000)
                .ToList();
        }

        public WorkflowJob GetWorkflowJob(string id)
        {
            return ddsInstrumentationContext.WorkflowJobs.Find(id);
        }

        public List<WorkflowJob> GetRecentErrors()
        {
            return ddsInstrumentationContext.WorkflowJobs
                .OrderByDescending(j => j.Created)
                .Where(j => j.Error != null)
                .Take(1000)
                .ToList();
        }


        public WorkflowCallStats GetStatsModel()
        {
            var end = DateTime.Now.AddMinutes(-10);
            var start = end.AddHours(0 - RecentSampleHours);
            var result = new WorkflowCallStats();
            
            // was: select top 10 * from WorkflowJobs where Finished=1 order by Taken desc
            result.RecentlyTaken = ddsInstrumentationContext.WorkflowJobs
                .Where(j => j.Finished)
                .OrderByDescending(j => j.Taken)
                .Take(10)
                .ToList();
            
            // was: select * from WorkflowJobs where Taken is not null and Finished=0
            result.TakenAndUnfinished = ddsInstrumentationContext.WorkflowJobs
                .Where(j => j.Taken != null && !j.Finished)
                .ToList();

            var command = ddsInstrumentationContext.Database.GetDbConnection().CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            var sql = @"
select COUNT (*) from workflow_jobs
select COUNT (*) from workflow_jobs where finished=1 
select SUM(CONVERT(BIGINT, words)) from workflow_jobs
select count(*) from workflow_jobs where taken > '$0' and taken <= '$1'
";
            const string sqlFormat = "yyyy-MM-dd HH:mm:ss.fff";
            sql = sql.Replace("$0", start.ToString(sqlFormat));
            sql = sql.Replace("$1", end.ToString(sqlFormat));
            command.CommandText = sql;
            var conn = ddsInstrumentationContext.Database.GetDbConnection();
            try
            {
                conn.Open();
                var reader = command.ExecuteReader();
                reader.Read();
                result.TotalJobs = (int) reader[0];
                reader.NextResult();
                reader.Read();
                result.FinishedJobs = (int) reader[0];
                if (result.FinishedJobs > 0 && result.TotalJobs > 0)
                {
                    result.FinishedPercent = (float)result.FinishedJobs / result.TotalJobs;
                }
                reader.NextResult();
                reader.Read();
                result.WordCount = (long) reader[0];
                reader.NextResult();
                reader.Read();
                var takenInPeriod = (int) reader[0];
                var jobsPerHour = (float)takenInPeriod / RecentSampleHours;
                var jobsLeft = result.TotalJobs - result.FinishedJobs;
                var hoursLeft = (float)jobsLeft / jobsPerHour;
                try
                {
                    result.EstimatedCompletion = DateTime.Now.AddHours(hoursLeft);
                }
                catch
                {
                    result.EstimatedCompletion = DateTime.MaxValue;
                }
            }
            finally
            {
                conn.Close();
            }
            return result;
        }
    }
}