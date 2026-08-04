using System;
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

    public List<DdsIdentity> DdsIdentifiers { get; set; }
    public string IdentifiersSummary { get; set; }

    public string Error { get; set; }
    public void TidyIdentifiers(IIdentityService identityService, bool populateList = false)
    {
        if (Identifiers.IsNullOrWhiteSpace())
        {
            DdsIdentifiers = new List<DdsIdentity>();
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
            DdsIdentifiers = new List<DdsIdentity>();
            var invalidLines = new List<string>();
            foreach (var line in lines)
            {
                try
                {
                    DdsIdentifiers.Add(identityService.GetIdentity(line));
                }
                catch (FormatException)
                {
                    invalidLines.Add(line);
                }
            }
            var bCount = DdsIdentifiers.Count(ddsId => ddsId.Source == Source.Sierra);
            IdentifiersSummary = $"{DdsIdentifiers.Count} identifiers of which {bCount} are from Sierra.";
            if (invalidLines.HasItems())
            {
                IdentifiersSummary += $" {invalidLines.Count} invalid identifier(s) ignored.";
                Error = $"Not valid identifiers, ignored: {string.Join(", ", invalidLines)}";
            }
        }
        Identifiers = string.Join('\n', lines);
    }
}
