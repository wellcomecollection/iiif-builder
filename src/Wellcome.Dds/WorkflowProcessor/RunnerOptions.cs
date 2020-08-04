namespace WorkflowProcessor
{
    /// <summary>
    /// A collection of options for controlling WorkflowRunner processing
    /// </summary>
    public class RunnerOptions
    {
        /// <summary>
        /// Create DLCS Job in database to be processed by DlcsJobProcessor.
        /// </summary>
        public bool RegisterImages { get; set; }
        
        /// <summary>
        /// Save metadata from Catalogue API to db
        /// </summary>
        public bool RefreshFlatManifestations { get; set; } 
        
        /// <summary>
        /// Create IIIF v3 and save to S3.
        /// </summary>
        public bool RebuildIIIF3 { get; set; }
        
        public bool RebuildTextCaches { get; set; } 
        
        /// <summary>
        /// Create W3C annotation and save to S3.
        /// </summary>
        public bool RebuildAllAnnoPageCaches { get; set; }
    }
}