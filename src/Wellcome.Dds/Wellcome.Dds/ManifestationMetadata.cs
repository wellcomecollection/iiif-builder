using System.Collections.Generic;
using Wellcome.Dds.Common;

namespace Wellcome.Dds
{
    public class ManifestationMetadata
    {
        public DdsIdentifier Identifier { get; set; }
        public List<Manifestation> Manifestations { get; set; }
        public List<Metadata> Metadata { get; set; }
    }
}