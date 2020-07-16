namespace WorkflowProcessor
{
    /// <summary>
    /// A collection of options for controlling WorkflowRunner processing
    /// </summary>
    public class RunnerOptions
    {
        public bool RegisterImages { get; set; }
        public bool RefreshFlatManifestations { get; set; }
        public bool RebuildPackageCaches { get; set; }
        public bool RebuildTextCaches { get; set; }
        public bool RebuildAllAnnoPageCaches { get; set; }
    }
}