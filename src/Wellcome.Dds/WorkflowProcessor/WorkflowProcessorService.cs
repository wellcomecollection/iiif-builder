using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.Common;

namespace WorkflowProcessor
{
    /// <summary>
    /// Background process that processes workflow_job records from database and creates dlcs_job record.
    /// </summary>
    public class WorkflowProcessorService : BackgroundService
    {
        private readonly ILogger<WorkflowProcessorService> logger;
        private readonly IServiceScopeFactory serviceScopeFactory;
        //private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly string[] knownPopulationOperations = {"--populate-file", "--populate-slice"};
        private readonly string[] workflowOptionsParam = {"--runnerOptions"};
        private readonly string FinishAllJobsParam = "--finish-all";

        public WorkflowProcessorService(
            ILogger<WorkflowProcessorService> logger,
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

        private (string operation, string parameter) GetOperationWithParameter(string[] lookingFor)
        {
            var arguments = Environment.GetCommandLineArgs();
            logger.LogInformation("GetCommandLineArgs: {0}", string.Join(", ", arguments));
            return StringUtils.GetOperationAndParameter(arguments, lookingFor);
        }

        private bool HasArgument(string arg)
        {
            return Environment.GetCommandLineArgs().Contains(arg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // if using launchSettings.json, you'll need to add something like
            // "commandLineArgs": "-- --populate-file /home/iiif-builder/src/Wellcome.Dds/WorkflowProcessor.Tests/examples.txt"
            // ...to run an examples file rather than default to job processing mode. 
            logger.LogInformation("Hosted service ExecuteAsync");
            var populationOperationWithParameter = GetOperationWithParameter(knownPopulationOperations);
            var workflowOptionsString = GetOperationWithParameter(workflowOptionsParam).parameter;
            int? workflowOptionsFlags = null;
            if(int.TryParse(workflowOptionsString, out var workflowOptionsValue))
            {
                workflowOptionsFlags = workflowOptionsValue;
            }

            if (HasArgument(FinishAllJobsParam))
            {
                int count = FinishAllJobs();
                logger.LogInformation($"Force-finished {count} workflow jobs");
            }
            switch (populationOperationWithParameter.operation)
            {
                // Population operations might be run from a desktop, against an RDS database.
                // Or deployed to a temporary container and run from there.
                case "--populate-file":
                    await PopulateJobsFromFile(populationOperationWithParameter.parameter, workflowOptionsFlags, stoppingToken);
                    break;
                
                case "--populate-slice":
                    await PopulateJobsFromSlice(populationOperationWithParameter.parameter, workflowOptionsFlags, stoppingToken);
                    break;
                
                default:
                    // This is what the workflowprocessor normally does! Takes jobs rather than creates them.
                    await PollForWorkflowJobs(stoppingToken);
                    break;
            }
            logger.LogInformation("Stopping WorkflowProcessorService...");
        }

        private async Task PopulateJobsFromSlice(string parameter, int? workflowOptionsValue, CancellationToken stoppingToken)
        {
        }

        private int FinishAllJobs()
        {
            using var scope = serviceScopeFactory.CreateScope();
            var workflowCallRepository = scope.ServiceProvider.GetRequiredService<IWorkflowCallRepository>();
            return workflowCallRepository.FinishAllJobs();
        }

        private async Task PopulateJobsFromFile(string file, int? workflowOptions, CancellationToken stoppingToken)
        {
            // https://stackoverflow.com/a/48368934
            using var scope = serviceScopeFactory.CreateScope();
            var workflowCallRepository = scope.ServiceProvider.GetRequiredService<IWorkflowCallRepository>();
            foreach (var bNumber in File.ReadLines(file))
            {
                if (bNumber.IsBNumber() && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation($"Attempting to create job for {bNumber}...");
                    var job = await workflowCallRepository.CreateWorkflowJob(bNumber, workflowOptions);
                    var displayString = "(none specified)";
                    if (job.WorkflowOptions.HasValue)
                    {
                        displayString = RunnerOptions.FromInt32(job.WorkflowOptions.Value).ToString();
                    }
                    logger.LogInformation($"Job {job.Identifier} created with options {displayString}");
                }
                else
                {
                    logger.LogInformation($"Skipping line, '{bNumber}' is not a b number.");
                }
            }
        }

        private async Task PollForWorkflowJobs(CancellationToken stoppingToken)
        {
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