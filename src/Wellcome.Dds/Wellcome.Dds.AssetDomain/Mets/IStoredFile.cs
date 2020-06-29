using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// This is like IPhysicalFile, but for files not mentioned in structmaps.
    /// It's used for Poster Images as a temporary measure, until the format of
    /// METS for poster image can be determined.
    /// </summary>
    public interface IStoredFile
    {
        IWorkStore WorkStore { get; set; }
        IAssetMetadata AssetMetadata { get; set; }
        string RelativePath { get; set; }
        IStoredFileInfo GetStoredFileInfo();
    }
}
