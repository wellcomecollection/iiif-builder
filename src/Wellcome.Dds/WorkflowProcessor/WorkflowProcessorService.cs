using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogueClient.ToolSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.Catalogue;
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
        private readonly DdsOptions ddsOptions;
        private readonly string[] knownPopulationOperations = {"--populate-file", "--populate-slice"};
        private readonly string[] workflowOptionsParam = {"--workflow-options"};
        private readonly string FinishAllJobsParam = "--finish-all";
        private readonly string TraverseChemistAndDruggistParam = "--chem";

        /// <summary>
        /// Usage:
        /// 
        /// (no args)
        /// Process workflow jobs from the table - standard continuous behaviour of this service.
        /// 
        /// --finish-all
        /// Mark all non-taken jobs as finished (reset them)
        /// 
        /// --populate-file {filepath}
        /// Create workflow jobs from the b numbers in a file
        /// 
        /// --populate-slice {skip}
        /// Create workflow jobs from a subset of all possible digitised b numbers.
        /// This will download and unpack the catalogue dump file, take every {skip} lines,
        /// produce a list of unique b numbers that have digital locations, then register jobs for them.
        /// e.g., skip 100 will populate 1% of the total possible jobs, skip 10 will populate 10%, skip 1 will do ALL jobs.
        /// 
        /// --workflow-options {flags-int}
        /// Optional argument for the two populate-*** operations.
        /// This will create a job with a set of processing options that will override the default RunnerOptions, when
        /// the job is picked up by the WorkflowProcessor.
        /// This flags integer can be obtained by creating a new RunnerOptions instance and calling ToInt32().
        /// There is also a helper RunnerOptions.AllButDlcsSync() call for large-scale operations.
        /// 
        /// --workflow-options 30
        /// This is (currently) the all-but-DLCS flags value.
        /// 
        /// --workflow-options 6
        /// RefreshFlatManifestations and RebuildIIIF (no text or image registration)
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="options"></param>
        public WorkflowProcessorService(
            ILogger<WorkflowProcessorService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<DdsOptions> options)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            this.ddsOptions = options.Value;
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
                return;
            }

            if (HasArgument(TraverseChemistAndDruggistParam))
            {
                logger.LogInformation("Print Chemist And Druggist Enumeration");
                await TraverseChemistAndDruggist();
                return;
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

        private async Task PopulateJobsFromSlice(string parameter, int? workflowOptions, CancellationToken stoppingToken)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var workflowCallRepository = scope.ServiceProvider.GetRequiredService<IWorkflowCallRepository>();
            var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogue>();
            if (!int.TryParse(parameter, out int skip))
            {
                throw new ArgumentException("Cannot parse skip integer", nameof(parameter));
            }
            var dumpLoopInfo = new DumpLoopInfo
            {
                Skip = skip, 
                Filter = DumpLoopInfo.IIIFLocationFilter
            };
            logger.LogInformation("Downloading dump file (will take several minutes");
            await DumpUtils.DownloadDump();
            logger.LogInformation("Unpacking dump file");
            DumpUtils.UnpackDump();
            DumpUtils.FindDigitisedBNumbers(dumpLoopInfo, catalogue);
            logger.LogInformation(
                $"{dumpLoopInfo.UniqueDigitisedBNumbers.Count} unique digitised b numbers found (from skip value of {skip})");
            int counter = 1;
            foreach (string uniqueDigitisedBNumber in dumpLoopInfo.UniqueDigitisedBNumbers)
            {
                await CreateNewWorkflowJob(workflowOptions, uniqueDigitisedBNumber, workflowCallRepository);
                logger.LogInformation($"({counter++} of {dumpLoopInfo.UniqueDigitisedBNumbers.Count})");
            }
            logger.LogInformation("Finished processing unique b numbers.");
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
                    await CreateNewWorkflowJob(workflowOptions, bNumber, workflowCallRepository);
                }
                else
                {
                    logger.LogInformation($"Skipping line, '{bNumber}' is not a b number.");
                }
            }
        }

        private async Task CreateNewWorkflowJob(int? workflowOptions, string bNumber,
            IWorkflowCallRepository workflowCallRepository)
        {
            logger.LogInformation($"Attempting to create job for {bNumber}...");
            var job = await workflowCallRepository.CreateWorkflowJob(bNumber, workflowOptions);
            var displayString = "(none specified)";
            if (job.WorkflowOptions.HasValue)
            {
                displayString = RunnerOptions.FromInt32(job.WorkflowOptions.Value).ToDisplayString();
            }

            logger.LogInformation($"Job {job.Identifier} created with options {displayString}");
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

                    using var scope = serviceScopeFactory.CreateScope();

                    var dbContext = scope.ServiceProvider.GetRequiredService<DdsInstrumentationContext>();

                    var jobId = dbContext.MarkFirstJobAsTaken(ddsOptions.MinimumJobAgeMinutes);
                    if (jobId == null)
                    {
                        waitMs = GetWaitMs(waitMs);
                        continue;
                    }

                    waitMs = 2;
                    var runner = GetWorkflowRunner(scope);
                    var job = await dbContext.WorkflowJobs.FindAsync(jobId);
                    await runner.ProcessJob(job, stoppingToken);
                    job.Finished = true;
                    try
                    {
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                    catch (DbUpdateException e)
                    {
                        throw new DdsInstrumentationDbException("Could not save workflow job: " + e.Message, e);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error running WorkflowProcessor");
                }
            }
        }
        
        
        private async Task TraverseChemistAndDruggist()
        {
            using var scope = serviceScopeFactory.CreateScope();
            var runner = GetWorkflowRunner(scope);
            await runner.TraverseChemistAndDruggist();
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