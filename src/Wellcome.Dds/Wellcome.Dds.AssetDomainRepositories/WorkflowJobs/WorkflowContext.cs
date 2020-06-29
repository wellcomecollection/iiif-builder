using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.AssetDomainRepositories.WorkflowJobs
{
    public class WorkflowContext : DbContext
    {
        public DbSet<WorkflowJob> WorkflowJobs { get; set; }
    }
}
