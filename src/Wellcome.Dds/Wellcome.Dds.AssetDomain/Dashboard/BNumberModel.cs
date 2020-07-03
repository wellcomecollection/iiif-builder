namespace Wellcome.Dds.AssetDomain.Dashboard
{
    // based on DashboardBNumber from old dashboard
    public class BNumberModel
    {
        /// <summary>
        /// The identifier used to initialise the model. Might be invalid.
        /// </summary>
        public string RawBNumber { get; set; }
        public string BNumber { get; set; }
        public string DisplayTitle { get; set; }

        public string EncoreRecordUrl { get; set; }
        public string ItemPageUrl { get; set; }
        public string ManifestUrl { get; set; }
        public string EncoreBiblioRecordUrl { get; set; }
        
    }
}
