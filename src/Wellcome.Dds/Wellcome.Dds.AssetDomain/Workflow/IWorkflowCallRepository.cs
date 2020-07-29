using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Workflow
{
    /// <summary>
    /// Refactored from GoobiCallSupport
    /// </summary>
    public interface IWorkflowCallRepository
    {
        List<WorkflowJob> GetRecent();
        WorkflowJob GetWorkflowJob(string id);
        List<WorkflowJob> GetRecentErrors();
        WorkflowCallStats GetStatsModel();

        Task<WorkflowJob> CreateWorkflowJob(string id);
    }
}