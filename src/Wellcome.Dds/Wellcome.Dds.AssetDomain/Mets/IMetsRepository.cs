using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsRepository
    {
        Task<IMetsResource> GetAsync(DdsIdentifier identifier);

        /// <summary>
        /// Get all manifestations for specified identifier (e.g. Volumes/Periodical)
        /// </summary>
        /// <param name="identifier">Manifest identifier to lookup</param>
        IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(DdsIdentifier identifier);
         
        Task<int> FindSequenceIndex(DdsIdentifier identifier);
    }
}
