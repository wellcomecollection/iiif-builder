using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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

        public static WellcomeBagAwareArchiveStorageMap FromJObject(JObject storageManifest, string identifier)
        {
            // This is the length of the substring "data/"
            const int dataPathElementOffset = 5;
            
            var accessLocation = storageManifest["location"];
            var bucketName = accessLocation["bucket"].Value<string>();
            var archiveStorageMap = new WellcomeBagAwareArchiveStorageMap
            {
                BucketName = bucketName,
                StorageManifestCreated = storageManifest["createdDate"].Value<DateTime>()
            };
            var pathSep = new [] { '/' };

            var versionToFiles = new Dictionary<string, HashSet<string>>();
            foreach (var file in storageManifest["manifest"]["files"])
            {
                // strip "data/"
                // This makes an assumption that the file layout follows an expected structure
                // That's a valid assumption for the DDS to make, but not any other application using the storage
                var relativePath = file["name"].Value<string>().Substring(dataPathElementOffset);
                var version = file["path"].Value<string>().Split(pathSep).First();
                // we no longer read this; we know how it is made.
                // var awsKey = PathStringUtils.Combine(accessLocationPath, file["path"].Value<string>());
                var minRelativePath = relativePath.Replace(identifier, "#");
                if (!versionToFiles.ContainsKey(version))
                {
                    versionToFiles[version] = new HashSet<string>();
                }
                versionToFiles[version].Add(minRelativePath);
            }
            // now order the dict by largest member
            archiveStorageMap.VersionSets = versionToFiles.OrderBy(kv => kv.Value.Count).ToList();
            archiveStorageMap.Built = DateTime.UtcNow;
            return archiveStorageMap;
        }
    }
}
