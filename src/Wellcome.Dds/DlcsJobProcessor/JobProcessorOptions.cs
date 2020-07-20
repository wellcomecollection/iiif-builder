namespace DlcsJobProcessor
{
    public class JobProcessorOptions
    {
        /// <summary>
        /// Number of seconds to wait before re-running job
        /// </summary>
        public int YieldTimeSecs { get; set; }
        
        /// <summary>
        /// Identifier filter to apply when fetching jobs.
        /// Valid for "processqueue" mode only. 
        /// </summary>
        public string Filter { get; set; }
        
        // TODO - validate this?
        /// <summary>
        /// The job mode "processqueue" or "updatestatus"
        /// </summary>
        public string Mode { get; set; }
    }
}