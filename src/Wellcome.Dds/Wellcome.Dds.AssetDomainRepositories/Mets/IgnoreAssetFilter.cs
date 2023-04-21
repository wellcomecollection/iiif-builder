using System.Collections.Generic;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class IgnoreAssetFilter : IIgnoreAssetFilter
    {
        public List<string?> GetStorageIdentifiersToIgnore(string manifestationType, List<IPhysicalFile> sequence)
        {
            var ignored = new List<string?>();
            if (manifestationType == "Video")
            {
                // A video with a poster image and/or an MXF master file.
                foreach (IPhysicalFile pf in sequence)
                {
                    if (!pf.MimeType.IsVideoMimeType())
                    {
                        ignored.Add(pf.StorageIdentifier);
                    }
                }
            }
            
            
            return ignored;
        }
    }
}
