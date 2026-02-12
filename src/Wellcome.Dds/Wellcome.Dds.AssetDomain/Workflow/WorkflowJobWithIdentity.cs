using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Workflow;

public class WorkflowJobWithIdentity
{
    public required WorkflowJob WorkflowJob { get; set; }
    public required DdsIdentity DdsIdentity { get; set; }
}