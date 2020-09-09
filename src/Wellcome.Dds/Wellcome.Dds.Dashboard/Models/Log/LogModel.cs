using System;

namespace Wellcome.Dds.Dashboard.Models.Log
{
    public class LogModel
    {
        public Uri JobProcessorUri { get; }
        public Uri WorkflowProcessorUri { get; }
        public Uri DashboardUri { get; set; }
        public Uri IIIFServerUri { get; set; }

        public LogModel(DashOptions options)
        {
            JobProcessorUri = options.GetJobProcessorLog();
            WorkflowProcessorUri = options.GetWorkflowProcessorLog();
            DashboardUri = options.GetDashboardLog();
            IIIFServerUri = options.GetIIIFServerLog();
        }

    }
}