using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds
{
    public interface IDds
    {
        List<Manifestation> GetByAssetType(string type);
        Dictionary<string, int> GetTotalsByAssetType();
        List<Manifestation> AutoComplete(string id);

        Task RefreshManifestations(string id);
    }
}
