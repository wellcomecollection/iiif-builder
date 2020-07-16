using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Workflow;

namespace WorkflowProcessor
{
    /// <summary>
    /// Main task runner for WorkflowProcessor
    /// </summary>
    public class WorkflowRunner
    {
        private readonly IIngestJobRegistry ingestJobRegistry;
        private readonly ILogger<WorkflowRunner> logger;
        private readonly RunnerOptions runnerOptions;

        public WorkflowRunner(IIngestJobRegistry ingestJobRegistry, IOptions<RunnerOptions> runnerOptions, ILogger<WorkflowRunner> logger)
        {
            this.ingestJobRegistry = ingestJobRegistry;
            this.logger = logger;
            this.runnerOptions = runnerOptions.Value;
        }

        public async Task ProcessJob(WorkflowJob job, CancellationToken cancellationToken = default)
        {
            job.Taken = DateTime.Now;

            try
            {
                if (runnerOptions.RegisterImages)
                {
                    var batchResponse = await ingestJobRegistry.RegisterImage(job.Identifier);
                    if (batchResponse.Length > 0)
                    {
                        job.FirstDlcsJobId = batchResponse[0].Id;
                        job.DlcsJobCount = batchResponse.Length;
                    }
                }
                
                job.TotalTime = (long)(DateTime.Now - job.Taken.Value).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                job.Error = ex.Message.SummariseWithEllipsis(240);
                logger.LogError(ex, "Error in workflow runner");
            }
        }
    }
}