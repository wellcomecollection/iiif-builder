using System;
using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Workflow
{
    public class WorkflowCallStats
    {
        public int TotalJobs { get; set; }
        public int FinishedJobs { get; set; }
        public float FinishedPercent { get; set; }
        public decimal WordCount { get; set; }
        public List<WorkflowJobWithIdentity>? RecentlyTaken { get; set; }
        public List<WorkflowJobWithIdentity>? TakenAndUnfinished { get; set; }
        public DateTime EstimatedCompletion { get; set; }
        public int RecentSampleHours { get; set; }
        public int Errors { get; set; }
    }
}