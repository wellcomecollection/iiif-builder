using System;
using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    [Serializable]
    public class WellcomeBagAwareArchiveStorageMap
    {
        // a List of: "v1" => { "alto/#_0001.xml", ... }
        // in decreasing order of size of set
        public List<KeyValuePair<string, HashSet<string>>> VersionSets;
        public string BucketName;
        public DateTime StorageManifestCreated;
        public DateTime Built;
    }
}
