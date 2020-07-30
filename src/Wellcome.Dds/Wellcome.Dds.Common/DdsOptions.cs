namespace Wellcome.Dds.Common
{
    // Which class Library does this class live in?
    public class DdsOptions
    {
        public string StatusContainer { get; set; }
        public string GoFile { get; set; }
        public string StatusProviderHeartbeat { get; set; }
        public string StatusProviderLogSpecialFile { get; set; }
        public string PersistentPlayerUri { get; set; }
        public string PersistentCatalogueRecord { get; set; }
        public string EncoreBibliographicData { get; set; }
        public string LinkedDataDomain { get; set; }
        public string ManifestTemplate { get; set; }
        public bool AvoidCaching { get; set; }
        public string EarliestJobDateTime { get; set; }
        public int MinimumJobAgeMinutes { get; set; }
        public string JobProcessorLog { get; set; }
        public string WorkflowProcessorLog { get; set; }
        public string DashBodyInject { get; set; } = "";
        public string GoobiCall { get; set; }

        // New, Catalogue
        public string ApiWorkTemplate { get; set; }

        // New, Dds
        public string DlcsOriginUsername { get; set; }
        public string DlcsOriginPassword { get; set; }
        public string PinVerifyUrlFormat { get; set; } // suggest this should be secret
    }
}
