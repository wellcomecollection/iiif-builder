using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsRepository
    {
        Task<IMetsResource> GetAsync(string identifier);

        IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(string identifier);
         
        Task<int> FindSequenceIndex(string identifier);
    }
}
