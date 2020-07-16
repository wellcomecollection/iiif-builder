using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wellcome.Dds.AssetDomainRepositories;

namespace WorkflowProcessor
{
    /// <summary>
    /// Background process that processes workflow_job records from database and creates dlcs_job record.
    /// </summary>
    public class WorkflowProcessorService : BackgroundService
    {
        private readonly ILogger<WorkflowProcessorService> logger;
        private readonly IServiceScopeFactory serviceScopeFactory;

        public WorkflowProcessorService(ILogger<WorkflowProcessorService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Hosted service ExecuteAsync");
            int waitMs = 2;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogDebug("Waiting for {wait} ms..", waitMs);
                    await Task.Delay(TimeSpan.FromMilliseconds(waitMs), stoppingToken);

                    var cutoff = DateTime.Now.AddMinutes(-1);

                    using var scope = serviceScopeFactory.CreateScope();

                    var dbContext = scope.ServiceProvider.GetRequiredService<DdsInstrumentationContext>();

                    var job = dbContext.WorkflowJobs
                        .Where(j => j.Waiting && j.Created < cutoff)
                        .OrderBy(j => j.Created)
                        .FirstOrDefault();

                    if (job == null)
                    {
                        waitMs = GetWaitMs(waitMs);
                        continue;
                    }

                    job.Waiting = false;
                    job.Taken = DateTime.Now;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    waitMs = 2;
                    var runner = scope.ServiceProvider.GetRequiredService<WorkflowRunner>();
                    await runner.ProcessJob(job, stoppingToken);
                    job.Finished = true;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // TODO - should we set back to waiting = true if failed to process? 
                    logger.LogError(ex, "Error running WorkflowProcessor");
                }
            }
        }

        private static int GetWaitMs(int waitMs)
        {
            waitMs *= 2;

            // don't wait more than 5 mins
            if (waitMs > 300000)
            {
                waitMs = 300000;
            }

            return waitMs;
        }
    }
}