using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IIgnoreAssetFilter
    {
        List<string?> GetStorageIdentifiersToIgnore(string manifestationType, List<IPhysicalFile> sequence);
    }
}
