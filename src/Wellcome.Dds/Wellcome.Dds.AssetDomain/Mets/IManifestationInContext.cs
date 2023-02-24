using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IManifestationInContext
    {
        IManifestation Manifestation { get; set; }
        string PackageIdentifier { get; set; }
        /// <summary>
        /// The manifestation's logical position within a sequence; THIS MIGHT NOT BE SET
        /// </summary>
        int SequenceIndex { get; set; }
        string? VolumeIdentifier { get; set; }
        string? IssueIdentifier { get; set; }
    }
}
