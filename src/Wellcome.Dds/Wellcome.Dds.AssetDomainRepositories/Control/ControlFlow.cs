using System;

namespace Wellcome.Dds.AssetDomainRepositories.Control
{
    /// <summary>
    /// Entity for monitoring background processing jobs.
    /// </summary>
    public class ControlFlow
    {
        /// <summary>
        /// Unique identifier for controlFlow job
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Get or set heartbeat for last operation
        /// </summary>
        public DateTime? Heartbeat { get; set; }
        
        /// <summary>
        /// Get or set dateTime control flow record created
        /// </summary>
        public DateTime CreatedOn { get; set; }
        
        /// <summary>
        /// Get or set dateTime control flow job stopped
        /// </summary>
        public DateTime? StoppedOn { get; set; }
    }
}