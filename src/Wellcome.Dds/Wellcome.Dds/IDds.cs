﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds
{
    public interface IDds
    {
        List<Manifestation> GetByAssetType(string type);
        Dictionary<string, int> GetTotalsByAssetType();
        List<Manifestation> AutoComplete(string id);

        Task RefreshManifestations(string id, Work work = null);

        ManifestationMetadata GetManifestationMetadata(string id);
    }
}
