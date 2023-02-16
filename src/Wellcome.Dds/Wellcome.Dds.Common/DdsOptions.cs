using System.ComponentModel.Design.Serialization;

namespace Wellcome.Dds.Common
{
    // Which class Library does this class live in?
    public class DdsOptions
    {
        /// <summary>
        /// The scheme and host that will be emitted when generating IIIF JSON
        /// E.g., https://iiif.wellcomecollection.org
        /// TODO - leave this with the name "LinkedDataDomain" for now, for ease of migration,
        /// but later, refactor to "SchemeAndHost"
        /// </summary>
        public string? LinkedDataDomain { get; set; }
        
        /// <summary>
        /// Used by Wellcome.Dds.Server or dashboard, just before the point of delivery, to replace
        /// the LinkedDataDomain value with an alternative - e.g., http://localhost:8084
        ///
        /// This allows loading previously created IIIF resources from stage or live buckets rather than
        /// having to create new IIIF if you need to point links to different locations.
        ///
        /// It also allows for A/B testing.
        /// This will not alter the path part of any URLs.
        /// </summary>
        public string? RewriteDomainLinksTo { get; set; }
        
        /// <summary>
        /// Similar to RewriteDomainLinksTo but for the DLCS. 
        /// This will not alter the path part of any URLs.
        /// </summary>
        public string? RewriteDlcsLinksHostTo { get; set; }
        
        /// <summary>
        /// For the DLCS you might want to point directly at a DLCS instance rather than through
        /// Cloudfront rewritten URLs. If this value is present (and > 0) the path part of the URL
        /// will be transformed, inserting the space value provided where necessary.
        /// </summary>
        public int? RewriteDlcsLinksSpaceTo { get; set; }
        
        public bool AvoidCaching { get; set; }
        public string? EarliestJobDateTime { get; set; }
        public int MinimumJobAgeMinutes { get; set; }
        
        public string? GoobiCall { get; set; }

        // New, Catalogue
        public string? ApiWorkTemplate { get; set; }
        public string? WellcomeCollectionApi { get; set; }

        // New, Dds
        public string? DlcsOriginUsername { get; set; }
        public string? DlcsOriginPassword { get; set; }

        // These FOUR can probably go, as we don't use these old APIs any more
        public string? PinVerifyUrlFormat { get; set; } // suggest this should be secret
        public string? PatronApiEndpoint { get; set; }
        public string? MillenniumUserName { get; set; }
        public string? MillenniumPassword { get; set; }
        
        public string? DlcsReturnUrl { get; set; }
        
        // TODO: Move the following 5 AWS settings to separate class
        // Buckets
        public string? PresentationContainer { get; set; }
        public string? TextContainer { get; set; }
        public string? AnnotationContainer { get; set; }
        
        // Workflow
        public string? WorkflowMessageQueue { get; set; }
        public bool WorkflowMessagePoll { get; set; }
        
        public bool ReferenceV0SearchService { get; set; }
        public bool UseRequiredStatement { get; set; }
        public bool BuildWholeManifestLineAnnotations { get; set; }
        
        public string? IncludeExtraAccessConditionsInManifest { get; set; }
        
        public int PlaceholderCanvasCacheTimeDays { get; set; }
    }
}
