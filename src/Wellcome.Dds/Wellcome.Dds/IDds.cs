using System.Collections.Generic;

namespace Wellcome.Dds
{
    public interface IDds
    {
        List<Manifestation> GetByAssetType(string type);
        Dictionary<string, int> GetTotalsByAssetType();
        List<Manifestation> AutoComplete(string id);
    }
}
