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
        /// <param name="id">If of workflow job to fetch.</param>
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

        Task<WorkflowJob> CreateWorkflowJob(string id, int? workflowOptions);

        int FinishAllJobs();
    }
}