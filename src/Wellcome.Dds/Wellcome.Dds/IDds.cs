using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds
{
    public interface IDds
    {
        List<Manifestation> GetByAssetType(string type);
        Dictionary<string, long> GetTotalsByAssetType();
        List<Manifestation> AutoComplete(string id);

        Task RefreshManifestations(DdsIdentifier ddsId, Work work = null);

        ManifestationMetadata GetManifestationMetadata(string id);
        List<Manifestation> GetManifestationsForChildren(string workReferenceNumber);

        Manifestation GetManifestation(string id);
    }
}
