using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Workflow;

public class WorkflowJobWithIdentity
{
    public required WorkflowJob WorkflowJob { get; set; }

    /// <summary>
    /// Null when the job's stored identifier could not be parsed (e.g. a legacy format);
    /// the job is still listed, without identity-derived links.
    /// </summary>
    public DdsIdentity? DdsIdentity { get; set; }
}