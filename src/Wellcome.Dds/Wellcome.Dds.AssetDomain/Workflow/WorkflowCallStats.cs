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
        public List<WorkflowJob> RecentlyTaken { get; set; }
        public List<WorkflowJob> TakenAndUnfinished { get; set; }
        public DateTime EstimatedCompletion { get; set; }
        public int RecentSampleHours { get; set; }
    }
}