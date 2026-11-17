using Wellcome.Dds.AssetDomain.Dlcs;

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
        /// Legacy sends maxUnauthorised, as for Protagonist up to 1.12. Explicit sends roles, maxWidth and
        /// openFullMax, and needs Protagonist 1.13.1 or later. See docs/dlcs-size-model.md.
        /// </summary>
        public DlcsSizeModel SizeModel { get; set; } = DlcsSizeModel.Legacy;

        /// <summary>
        /// Explicit only: the maxWidth for open images whose METS doesn't permit wholeImageHighResAsJpg.
        /// 1000 lets anyone zoom in, with no single response over 1000px; 0 leaves them unlimited.
        /// </summary>
        public int RestrictedDownloadMaxWidth { get; set; } = 1000;

        /// <summary>
        /// If true, syncs still work out role and size changes and list them as mismatches, but don't send them.
        /// For the DLCS cutover and the days after it.
        /// </summary>
        public bool HoldAccessChanges { get; set; } = false;

        /// <summary>
        /// Default timeout (in ms) use for HttpClient.Timeout.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;
        
        public string? PortalPageTemplate { get; set; }
        public string? PortalBatchTemplate { get; set; }
        public string? SingleAssetManifestTemplate { get; set; }
    }
}
