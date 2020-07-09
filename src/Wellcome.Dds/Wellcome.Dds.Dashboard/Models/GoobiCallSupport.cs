using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;

namespace Wellcome.Dds.Dashboard.Models
{
    public class GoobiCallSupport
    {
        private DdsInstrumentationContext ddsInstrumentationContext;

        public GoobiCallSupport(DdsInstrumentationContext ddsInstrumentationContext)
        {
            this.ddsInstrumentationContext = ddsInstrumentationContext;
        }

        public const int RecentSampleHours = 2;

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


        public GoobiCallStats GetStatsModel()
        {
            var end = DateTime.Now.AddMinutes(-10);
            var start = end.AddHours(0 - RecentSampleHours);
            var result = new GoobiCallStats();

            var command = ddsInstrumentationContext.Database.Connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            var sql = @"
select COUNT (*) from WorkflowJobs
select COUNT (*) from WorkflowJobs where Finished=1 
select SUM(CONVERT(BIGINT, Words)) from WorkflowJobs
select top 10 * from WorkflowJobs where Finished=1 order by Taken desc
select * from WorkflowJobs where Taken is not null and Finished=0
select count(*) from WorkflowJobs where Taken > '$0' and Taken <= '$1'
";
            const string sqlFormat = "yyyy-MM-dd HH:mm:ss.fff";
            sql = sql.Replace("$0", start.ToString(sqlFormat));
            sql = sql.Replace("$1", end.ToString(sqlFormat));
            command.CommandText = sql;
            try
            {
                ddsInstrumentationContext.Database.Connection.Open();
                var reader = command.ExecuteReader();
                var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
                result.TotalJobs = objectContext.Translate<int>(reader).First();
                reader.NextResult();
                result.FinishedJobs = objectContext.Translate<int>(reader).First();
                reader.NextResult();
                result.WordCount = objectContext.Translate<long>(reader).First();
                reader.NextResult();
                result.RecentlyTaken = objectContext.Translate<WorkflowJob>(reader).ToList();
                reader.NextResult();
                result.TakenAndUnfinished = objectContext.Translate<WorkflowJob>(reader).ToList();
                if (result.FinishedJobs > 0 && result.TotalJobs > 0)
                {
                    result.FinishedPercent = (float)result.FinishedJobs / result.TotalJobs;
                }
                reader.NextResult();
                var takenInPeriod = objectContext.Translate<int>(reader).First();
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
                ddsInstrumentationContext.Database.Connection.Close();
            }
            return result;
        }

        public static string GetAltoRate(WorkflowJob job)
        {
            if (job.TextPages <= 0 || job.TimeSpentOnTextPages <= 0)
            {
                return "n/a";
            }
            return ((1000 * job.TextPages) / job.TimeSpentOnTextPages).ToString();
        }
    }

    public class GoobiCallStats
    {
        public int TotalJobs { get; set; }
        public int FinishedJobs { get; set; }
        public float FinishedPercent { get; set; }
        public long WordCount { get; set; }
        public List<WorkflowJob> RecentlyTaken { get; set; }
        public List<WorkflowJob> TakenAndUnfinished { get; set; }
        public DateTime EstimatedCompletion { get; set; }
    }
}