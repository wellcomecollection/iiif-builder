using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsRepository
    {
        Task<IMetsResource> GetAsync(string identifier);

        /// <summary>
        /// Get all manifestations for specified identifier (e.g. Volumes/Periodical)
        /// </summary>
        /// <param name="identifier">Manifest identifier to lookup</param>
        IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(string identifier);
         
        Task<int> FindSequenceIndex(string identifier);
    }
}
