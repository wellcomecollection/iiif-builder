using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Workflow
{
    /// <summary>
    /// Methods for interacting with WorkflowJobs
    /// </summary>
    /// <remarks>Refactored from GoobiCallSupport</remarks>
    public interface IWorkflowCallRepository
    {
        /// <summary>
        /// Get the most recent WorkflowJobs
        /// </summary>
        /// <param name="count">The number of workflow jobs to return</param>
        Task<List<WorkflowJob>> GetRecent(int count = 1000);
        
        /// <summary>
        /// Get specified WorkflowJob.
        /// </summary>
        /// <param name="id">Id of workflow job to fetch.</param>
        ValueTask<WorkflowJob> GetWorkflowJob(string id);
        
        /// <summary>
        /// Get the most recent WorkflowJobs
        /// </summary>
        /// <param name="count">The number of workflow jobs to return</param>
        Task<List<WorkflowJob>> GetRecentErrors(int count = 1000);
        
        /// <summary>
        /// Get stats related for WorkflowCalls.
        /// </summary>
        /// <returns></returns>
        Task<WorkflowCallStats> GetStatsModel();

        /// <summary>
        /// Create a new workflow_job with specified workflowOptions.
        /// </summary>
        /// <param name="id">Identifier of work to create job for.</param>
        /// <param name="workflowOptions">Workflow options to use when processing job</param>
        /// <returns>Created <see cref="WorkflowJob"/> object.</returns>
        /// <remarks>See Wellcome.Dds.Common.RunnerOptions and associated tests for workflowOptions values</remarks>
        Task<WorkflowJob> CreateWorkflowJob(string id, int? workflowOptions);

        /// <summary>
        /// Create a new expedited workflow_job with specified workflowOptions.
        /// </summary>
        /// <param name="id">Identifier of work to create job for.</param>
        /// <param name="workflowOptions">Workflow options to use when processing job</param>
        /// <param name="invalidateCache">If true, caches for this object will be invalidated after processing</param>
        /// <returns>Created <see cref="WorkflowJob"/> object.</returns>
        /// <remarks>See Wellcome.Dds.Common.RunnerOptions and associated tests for workflowOptions values</remarks>
        Task<WorkflowJob> CreateExpeditedWorkflowJob(string id, int? workflowOptions, bool invalidateCache);

        int FinishAllJobs();
    }
}