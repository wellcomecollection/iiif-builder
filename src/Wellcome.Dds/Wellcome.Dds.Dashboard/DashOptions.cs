using System;

namespace Wellcome.Dds.Dashboard
{
    /// <summary>
    /// Configuration options specific to Dashboard only.
    /// </summary>
    public class DashOptions
    {
        /// <summary>
        /// The name of the job-processor service
        /// </summary>
        public string JobProcessorName { get; set; }
        
        /// <summary>
        /// The name of the workflow-processor service
        /// </summary>
        public string WorkflowProcessorName { get; set; }
        
        /// <summary>
        /// Optional css to inject to dashboard body
        /// </summary>
        public string DashBodyInject { get; set; } = "";

        /// <summary>
        /// Format for logging URL. Used in conjunction with JobProcessorName and WorkflowProcessorName to generate url. 
        /// </summary>
        public string LoggingFormat { get; set; }

        public Uri GetJobProcessorLog()
            => new Uri(string.Format(LoggingFormat, JobProcessorName));
        
        public Uri GetWorkflowProcessorLog()
            => new Uri(string.Format(LoggingFormat, WorkflowProcessorName));
    }
}