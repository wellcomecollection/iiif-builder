using System.Collections.Generic;
using Wellcome.Dds.Common;

namespace Wellcome.Dds
{
    public class ManifestationMetadata
    {
        public ManifestationMetadata(
            DdsIdentifier identifier, 
            List<Manifestation> manifestations, 
            List<Metadata> metadata)
        {
            Identifier = identifier;
            Manifestations = manifestations;
            Metadata = metadata;
        }

        public DdsIdentifier Identifier { get; set; }
        public List<Manifestation> Manifestations { get; set; }
        public List<Metadata> Metadata { get; set; }
    }
}