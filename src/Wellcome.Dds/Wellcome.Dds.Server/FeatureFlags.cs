namespace Wellcome.Dds.Server
{
    public enum FeatureFlags
    {
        /// <summary>
        /// Feature enabling all text services, including search.
        /// </summary>
        TextServices,
        
        /// <summary>
        /// Feature enabling all Presentation services for serving static content.
        /// </summary>
        PresentationServices
    }
}