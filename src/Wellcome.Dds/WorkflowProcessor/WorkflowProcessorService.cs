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

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Verifying WorkflowProcessor configuration");
                using var scope = serviceScopeFactory.CreateScope();
                GetWorkflowRunner(scope);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Failed to resolve WorkflowRunner object, aborting");
                throw;
            }
            
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            logger.LogInformation("GetCommandLineArgs: {0}", string.Join(", ", arguments));
            logger.LogInformation("Hosted service ExecuteAsync");
            int waitMs = 2;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogDebug("Waiting for {wait} ms..", waitMs);
                    await Task.Delay(TimeSpan.FromMilliseconds(waitMs), stoppingToken);

                    // TODO - temp
                    var cutoff = DateTime.Now; //
                    // var cutoff = DateTime.Now.AddMinutes(-1);

                    using var scope = serviceScopeFactory.CreateScope();

                    var dbContext = scope.ServiceProvider.GetRequiredService<DdsInstrumentationContext>();

                    // TODO - have the job returned as Taken, with the transaction in PostgreSQL,
                    // so there's no chance of two processes picking the same job.
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
                    var runner = GetWorkflowRunner(scope);
                    await runner.ProcessJob(job, stoppingToken);
                    job.Finished = true;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error running WorkflowProcessor");
                }
            }
            
            logger.LogInformation("Stopping WorkflowProcessorService...");
        }

        private static WorkflowRunner GetWorkflowRunner(IServiceScope scope) =>
            scope.ServiceProvider.GetRequiredService<WorkflowRunner>();

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