using System.Collections.Generic;
using System.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Models;

public class BulkWorkflowModel
{
    public string Identifiers { get; set; }
    public RunnerOptions RunnerOptions { get; set; }
    
    public List<WorkflowJob> WorkflowJobs { get; set; }

    public List<DdsIdentifier> DdsIdentifiers { get; set; }
    public string IdentifiersSummary { get; set; }

    public string Error { get; set; }
    public void TidyIdentifiers(bool populateList = false)
    {
        if (Identifiers.IsNullOrWhiteSpace())
        {
            DdsIdentifiers = new List<DdsIdentifier>();
            return;
        }
        
        var lines = Identifiers
            .SplitByDelimiter('\n')
            .Select(s => s.Trim())
            .Select(s => s.Replace(",", ""))
            .Select(s => s.Replace("\"",""))
            .Select(s => s.Replace("|", ""))
            .Where(s => s.HasText())
            .SelectMany(s => s.SplitByDelimiter(' '))
            .Select(s => s.Trim())
            .Where(s => s.HasText())
            .ToList();

        if (populateList)
        {
            DdsIdentifiers = lines.Select(s => new DdsIdentifier(s)).ToList();
            var bCount = DdsIdentifiers.Count(ddsId => ddsId.HasBNumber);
            IdentifiersSummary = $"{DdsIdentifiers.Count} identifiers of which {bCount} are (or have) B numbers.";
        }
        Identifiers = string.Join('\n', lines);
    }
}
