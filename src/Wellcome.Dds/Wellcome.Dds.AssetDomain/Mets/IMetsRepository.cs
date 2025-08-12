using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsRepository
    {
        Task<IMetsResource?> GetAsync(DdsIdentity identifier);

        /// <summary>
        /// Get all manifestations for specified identifier (e.g. Volumes/Periodical)
        /// </summary>
        /// <param name="identifier">Manifest identifier to lookup</param>
        IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(DdsIdentity identifier);
         
        Task<int> FindSequenceIndex(DdsIdentity identifier);
    }
}
