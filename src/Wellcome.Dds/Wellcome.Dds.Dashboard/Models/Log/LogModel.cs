using System;

namespace Wellcome.Dds.Dashboard.Models.Log
{
    public class LogModel
    {
        public Uri JobProcessorUri { get; }
        public Uri WorkflowProcessorUri { get; }

        public LogModel(DashOptions options)
        {
            JobProcessorUri = options.GetJobProcessorLog();
            WorkflowProcessorUri = options.GetWorkflowProcessorLog();
        }
    }
}