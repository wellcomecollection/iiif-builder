﻿namespace Wellcome.Dds.Common
{
    // Which class Library does this class live in?
    public class DdsOptions
    {
        // TODO - leave this with the name "LinkedDataDomain" for now, for ease of migration,
        // but later, refactor to "SchemeAndHost"
        public string LinkedDataDomain { get; set; }
        public string RewriteDomainLinksTo { get; set; }
        
        public bool AvoidCaching { get; set; }
        public string EarliestJobDateTime { get; set; }
        public int MinimumJobAgeMinutes { get; set; }
        
        public string GoobiCall { get; set; }

        // New, Catalogue
        public string ApiWorkTemplate { get; set; }
        public string WellcomeCollectionApi { get; set; }

        // New, Dds
        public string DlcsOriginUsername { get; set; }
        public string DlcsOriginPassword { get; set; }

        // These FOUR can probably go, as we don't use these old APIs any more
        public string PinVerifyUrlFormat { get; set; } // suggest this should be secret
        public string PatronApiEndpoint { get; set; }
        public string MillenniumUserName { get; set; }
        public string MillenniumPassword { get; set; }
        
        public string DlcsReturnUrl { get; set; }
        
        // TODO: Move the following 5 AWS settings to separate class
        // Buckets
        public string PresentationContainer { get; set; }
        public string TextContainer { get; set; }
        public string AnnotationContainer { get; set; }
        
        // Workflow
        public string WorkflowMessageQueue { get; set; }
        public bool WorkflowMessagePoll { get; set; }
        
        public bool ReferenceV0SearchService { get; set; }
        public bool UseRequiredStatement { get; set; }
        public bool BuildWholeManifestLineAnnotations { get; set; }
    }
}
