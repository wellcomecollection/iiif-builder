using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Workflow
{
    [Index(nameof(Created))]
    [Index(nameof(Expedite))]
    public class WorkflowJob
    {
        [Key] public string Identifier { get; set; } = null!;
        
        public DateTime? IngestJobStarted { get; set; }
        
        public bool ForceTextRebuild { get; set; }
        public bool Waiting { get; set; }
        public bool Finished { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Taken { get; set; }
        public int FirstDlcsJobId { get; set; }
        public int DlcsJobCount { get; set; }
        public int ExpectedTexts { get; set; }
        public int TextsAlreadyOnDisk { get; set; }
        public int TextsBuilt { get; set; }
        public int AnnosAlreadyOnDisk { get; set; }
        public int AnnosBuilt { get; set; }
        public long PackageBuildTime { get; set; }
        public long TextAndAnnoBuildTime { get; set; }
        public long TotalTime { get; set; }
        public string? Error { get; set; }
        public int Words { get; set; }
        public int TextPages { get; set; }
        public int TimeSpentOnTextPages { get; set; }
        public int? WorkflowOptions { get; set; }
        
        /// <summary>
        /// Signifies job should be processed asap, without need to wait until it is X age.
        /// </summary>
        public bool Expedite { get; set; }
        
        /// <summary>
        /// If true, caches should be flushed after processing.
        /// </summary>
        public bool FlushCache { get; set; }

        public string GetAltoRate()
        {
            if (TextPages <= 0 || TimeSpentOnTextPages <= 0)
            {
                return "n/a";
            }
            return ((1000 * TextPages) / TimeSpentOnTextPages).ToString();
        }

        public override string ToString()
        {
            return $"{Identifier}: {Created}, options: {WorkflowOptions}";
        }

        public string PrintState()
        {
            var sb = new StringBuilder();
            sb.Append(Identifier);
            sb.Append(": ");
            sb.Append("; WorkflowOptions: ");
            if (WorkflowOptions.HasValue)
            {
                sb.Append(RunnerOptions.FromInt32(WorkflowOptions.Value).ToDisplayString());
            }
            else
            {
                sb.Append("(all)");
            }
            sb.Append("; Waiting: ");
            sb.Append(Waiting);
            sb.Append("; Finished: ");
            sb.Append(Finished);
            sb.Append("; Created: ");
            sb.Append(Created);
            sb.Append("; Taken: ");
            sb.Append(Taken);
            sb.Append("; FirstDlcsJobId: ");
            sb.Append(FirstDlcsJobId);
            sb.Append("; DlcsJobCount: ");
            sb.Append(DlcsJobCount);
            sb.Append("; DlcsJobCount: ");
            sb.Append(DlcsJobCount);
            return sb.ToString();
        }
    }
}
