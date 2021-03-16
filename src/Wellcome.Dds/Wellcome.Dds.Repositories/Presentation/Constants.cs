namespace Wellcome.Dds.Repositories.Presentation
{
    /// <summary>
    /// A collection of string constants used in multiple places
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Default value for Attribution and Usage etc.
        /// </summary>
        public const string WellcomeCollection = "Wellcome Collection";

        /// <summary>
        /// wellcomecollection.org
        /// </summary>
        public const string WellcomeCollectionUri = "https://wellcomecollection.org";

        /// <summary>
        /// The Conditions name for items in copyright.
        /// </summary>
        public const string InCopyrightCondition = "In copyright";

        /// <summary>
        /// The Conditions of use for In Copyright material.
        /// </summary>
        public const string InCopyrightStatement =
            "Conditions of use: it is possible this item is protected by copyright and/or related rights. You are free to use this item in any way that is permitted by the copyright and related rights legislation that applies to your use. For other uses you need to obtain permission from the rights-holder(s).";

        /// <summary>
        /// Condition name for items where copyright not cleared.
        /// </summary>
        public const string CopyrightNotClearedCondition = "Copyright not cleared";

        /// <summary>
        /// Conditions of use for Copyright not cleared material.
        /// </summary>
        public const string CopyrightNotClearedStatement =
            "The copyright of this item has not been evaluated. Please refer to the original publisher/creator of this item for more information. You are free to use this item in any way that is permitted by the copyright and related rights legislation that applies to your use. <br/>See <a target=\"_top\" href=\"http://rightsstatements.org/page/CNE/1.0/?language=en\">rightsstatements.org</a> for more information.";

        /// <summary>
        /// Profile strings for external services
        /// </summary>
        public static class Profiles
        {
            /// <summary>
            /// Profile for tracking-extensions service
            /// </summary>
            public const string TrackingExtension = "http://universalviewer.io/tracking-extensions-profile";
            
            /// <summary>
            /// Profile for access-control-hints service
            /// </summary>
            public const string AccessControlHints = "http://wellcomelibrary.org/ld/iiif-ext/access-control-hints";
            
            /// <summary>
            /// Profile for build timestamp service
            /// </summary>
            public const string BuilderTime = "https://github.com/wellcomecollection/iiif-builder/build-timestamp";
        }
    }
}