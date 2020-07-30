using System;
using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    /// <summary>
    /// Stores all individual files related to a bNumber, split by version.
    /// </summary>
    /// <remarks>See https://gist.github.com/tomcrane/441f82c635292c737323e98d6340f8df</remarks>
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
