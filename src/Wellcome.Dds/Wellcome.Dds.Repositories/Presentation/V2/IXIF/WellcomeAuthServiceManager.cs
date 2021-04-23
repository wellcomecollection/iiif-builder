using System.Collections.Generic;
using System.Linq;
using IIIF;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// Helper class for managing <see cref="WellcomeAccessControlHintService"/> when converting to P2
    /// </summary>
    public class WellcomeAuthServiceManager
    {
        private Dictionary<string, WellcomeAccessControlHintService> wellcomeAuthServices = new();
        private Dictionary<string, V2ServiceReference>? requested = new();
        
        /// <summary>
        /// Returns true if services have been added, else false
        /// </summary>
        public bool HasItems { get; private set; }

        /// <summary>
        /// Add authService to internal collection.
        /// </summary>
        public void Add(WellcomeAccessControlHintService accessControlHintService)
        {
            HasItems = true;
            wellcomeAuthServices.Add(accessControlHintService.AuthService!.First().Id, accessControlHintService);
        }
        
        /// <summary>
        /// Get <see cref="IService"/> for specified id.
        /// This may be a full Service, or a ServiceReference depending on whether it has been used on
        /// manifest already 
        /// </summary>
        public IService Get(string id, bool forceFullService = false)
        {
            // if id has already been requested, return the serviceReference
            if (!forceFullService && requested.TryGetValue(id, out var serviceReference))
                return serviceReference;

            // else, create and save a new ServiceReference that will be returned in subsequent calls
            var newServiceReference = new V2ServiceReference {Id = id};
            requested[id] = newServiceReference;
            
            // Return the full Service element
            return wellcomeAuthServices[id];
        }
    }
}