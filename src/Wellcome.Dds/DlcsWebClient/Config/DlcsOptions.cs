namespace DlcsWebClient.Config
{
    public class DlcsOptions
    {
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int CustomerDefaultSpace { get; set; }
        public string? ApiEntryPoint { get; set; }
        public string? GlobalDlcsUrl { get; set; } = "https://api.dlcs.io/";
        public string? ResourceEntryPoint { get; set; }
        public string? InternalResourceEntryPoint { get; set; } = "https://dlcs.io/";
        public int BatchSize { get; set; } = 100;
        public string? SkeletonNamedQueryTemplate { get; set; }
        public bool PreventSynchronisation { get; set; } = false;
        public string PdfQueryName { get; set; } = "pdf";

        /// <summary>
        /// Default timeout (in ms) use for HttpClient.Timeout.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Whether to call the DLCS using old Deliverator AssetFamily, or protagonist delivery channels
        /// </summary>
        public bool SupportsDeliveryChannels { get; set; } = true;
        
        public string? PortalPageTemplate { get; set; }
        public string? PortalBatchTemplate { get; set; }
        public string? SingleAssetManifestTemplate { get; set; }
    }
}
