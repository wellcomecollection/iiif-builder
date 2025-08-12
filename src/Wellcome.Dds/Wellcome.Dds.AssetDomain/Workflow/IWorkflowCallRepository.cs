using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Common;

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
        ValueTask<WorkflowJob?> GetWorkflowJob(string id);
        
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
        /// <param name="ddsId">Identifier of work to create job for.</param>
        /// <param name="workflowOptions">Workflow options to use when processing job</param>
        /// <returns>Created <see cref="WorkflowJob"/> object.</returns>
        /// <remarks>See Wellcome.Dds.Common.RunnerOptions and associated tests for workflowOptions values</remarks>
        Task<WorkflowJob> CreateWorkflowJob(string ddsId, int? workflowOptions);

        /// <summary>
        /// Create a new expedited workflow_job with specified workflowOptions.
        /// </summary>
        /// <param name="ddsId">Identifier of work to create job for.</param>
        /// <param name="workflowOptions">Workflow options to use when processing job</param>
        /// <param name="invalidateCache">If true, caches for this object will be invalidated after processing</param>
        /// <returns>Created <see cref="WorkflowJob"/> object.</returns>
        /// <remarks>See Wellcome.Dds.Common.RunnerOptions and associated tests for workflowOptions values</remarks>
        Task<WorkflowJob> CreateExpeditedWorkflowJob(string ddsId, int? workflowOptions, bool invalidateCache);

        /// <summary>
        /// Create a new job from a received message (e.g., from an external queue)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="expedite"></param>
        /// <returns></returns>
        Task<WorkflowJob> CreateWorkflowJob(WorkflowMessage message, bool expedite = false);

        int FinishAllJobs();
        
        /// <summary>
        /// Return the number of jobs with errors that match this string (LIKE behaviour)
        /// </summary>
        /// <param name="msg">The error message, or part of it</param>
        /// <returns></returns>
        Task<int> CountMatchingErrors(string msg);

        /// <summary>
        /// Reset any jobs that have this error.
        /// </summary>
        /// <param name="resetWithMessage"></param>
        /// <returns></returns>
        Task<int> ResetJobsMatchingError(string resetWithMessage);

        Task DeleteJob(string ddsId);
    }
}