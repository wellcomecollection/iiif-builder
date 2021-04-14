using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wellcome.Dds.AssetDomainRepositories.Workflow
{
    public class WorkflowCallRepository : IWorkflowCallRepository
    {
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly ILogger<WorkflowCallRepository> logger;

        public WorkflowCallRepository(
            DdsInstrumentationContext ddsInstrumentationContext, 
            ILogger<WorkflowCallRepository> logger)
        {
            this.ddsInstrumentationContext = ddsInstrumentationContext;
            this.logger = logger;
        }

        private const int RecentSampleHours = 2;

        public async Task<WorkflowJob> CreateWorkflowJob(string id, int? workflowOptions)
        {
            var workflowJob = await ddsInstrumentationContext.PutJob(id, true, false, workflowOptions, false, false);
            return workflowJob;
        }

        public async Task<WorkflowJob> CreateExpeditedWorkflowJob(string id, int? workflowOptions, bool invalidateCache)
        {
            var workflowJob =
                await ddsInstrumentationContext.PutJob(id, true, false, workflowOptions, true, invalidateCache);
            return workflowJob;
        }

        public Task<List<WorkflowJob>> GetRecent(int count = 1000)
            => ddsInstrumentationContext.WorkflowJobs
                .OrderByDescending(j => j.Created)
                .Take(count)
                .ToListAsync();

        public ValueTask<WorkflowJob> GetWorkflowJob(string id) => ddsInstrumentationContext.WorkflowJobs.FindAsync(id);

        public Task<List<WorkflowJob>> GetRecentErrors(int count = 1000)
            => ddsInstrumentationContext.WorkflowJobs
                .OrderByDescending(j => j.Created)
                .Where(j => j.Error != null)
                .Take(count)
                .ToListAsync();

        public async Task<WorkflowCallStats> GetStatsModel()
        {
            var end = DateTime.Now.AddMinutes(-10);
            var start = end.AddHours(-RecentSampleHours);
            var result = new WorkflowCallStats { RecentSampleHours = RecentSampleHours };
            
            // was: select top 10 * from WorkflowJobs where Finished=1 order by Taken desc
            result.RecentlyTaken = await ddsInstrumentationContext.WorkflowJobs
                .Where(j => j.Finished)
                .OrderByDescending(j => j.Taken)
                .Take(10)
                .ToListAsync();
            
            // was: select * from WorkflowJobs where Taken is not null and Finished=0
            result.TakenAndUnfinished = await ddsInstrumentationContext.WorkflowJobs
                .Where(j => j.Taken != null && !j.Finished)
                .ToListAsync();

            await PopulateStats(start, end, result);

            return result;
        }

        private async Task PopulateStats(DateTime start, DateTime end, WorkflowCallStats result)
        {
            var connection = ddsInstrumentationContext.Database.GetDbConnection();

            var command = GetSqlCommand(connection, start, end);
            try
            {
                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                await reader.ReadAsync();
                result.TotalJobs = (int) reader[0];
                await reader.NextResultAsync();
                await reader.ReadAsync();
                result.FinishedJobs = (int) reader[0];
                if (result.FinishedJobs > 0 && result.TotalJobs > 0)
                {
                    result.FinishedPercent = (float) result.FinishedJobs / result.TotalJobs;
                }

                await reader.NextResultAsync();
                await reader.ReadAsync();
                result.WordCount = (decimal) reader[0];
                await reader.NextResultAsync();
                await reader.ReadAsync();
                var takenInPeriod = (int) reader[0];
                var jobsPerHour = (float) takenInPeriod / RecentSampleHours;
                var jobsLeft = result.TotalJobs - result.FinishedJobs;
                var hoursLeft = jobsLeft / jobsPerHour;
                try
                {
                    result.EstimatedCompletion = DateTime.Now.AddHours(hoursLeft);
                }
                catch
                {
                    result.EstimatedCompletion = DateTime.MaxValue;
                }
                await reader.NextResultAsync();
                await reader.ReadAsync();
                result.Errors = (int) reader[0];
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting workflow stats");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static DbCommand GetSqlCommand(DbConnection connection, DateTime start, DateTime end)
        {
            var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            var sql = @"
SELECT (COUNT (1)::int) FROM workflow_jobs;
SELECT (COUNT (1)::int) FROM workflow_jobs WHERE finished=true;
SELECT SUM(words::BIGINT) FROM workflow_jobs;
SELECT (COUNT(1)::int) FROM workflow_jobs WHERE taken BETWEEN '$0' AND '$1';
SELECT (COUNT(1)::int) FROM workflow_jobs WHERE error is not null;
";
            const string sqlFormat = "yyyy-MM-dd HH:mm:ss.fff";
            sql = sql.Replace("$0", start.ToString(sqlFormat));
            sql = sql.Replace("$1", end.ToString(sqlFormat));
            command.CommandText = sql;
            return command;
        }

        public int FinishAllJobs()
        {
            return ddsInstrumentationContext.FinishAllJobs();
        }
    }
}