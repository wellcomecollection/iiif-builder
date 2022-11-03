using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class ManifestationInContext : IManifestationInContext
    {
        public IManifestation Manifestation { get; set; }
        public string PackageIdentifier { get; set; }
        public int SequenceIndex { get; set; }
        public string VolumeIdentifier { get; set; }
        public string IssueIdentifier { get; set; }
    }
}
