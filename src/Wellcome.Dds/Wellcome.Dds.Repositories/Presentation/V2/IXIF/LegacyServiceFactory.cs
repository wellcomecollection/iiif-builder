using IIIF;
using IIIF.Presentation.V3.Content;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// Helper class for getting legacy services.
    /// These are non-standard P2 IIIF services required for backwards compatibility
    /// </summary>
    public static class LegacyServiceFactory
    {
        /// <summary>
        /// Get <see cref="IService"/> for specified <see cref="ExternalResource"/>
        /// </summary>
        public static IService? GetLegacyService(DdsIdentifier identifier, ExternalResource externalResource) =>
            externalResource.Profile switch
            {
                Constants.Profiles.TrackingExtension => new TrackingExtensionsService(identifier,
                    externalResource.Label!),
                Constants.Profiles.AccessControlHints => new AccessControlHints(identifier, externalResource.Label!),
                _ => null
            };
    }
}