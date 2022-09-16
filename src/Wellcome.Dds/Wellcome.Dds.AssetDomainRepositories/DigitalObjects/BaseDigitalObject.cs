using Wellcome.Dds.AssetDomain.DigitalObjects;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    public abstract class BaseDigitalObject : IDigitalObject
    {
        public string Identifier { get; set; }
        public bool? InSyncWithDlcs { get; set; }
        public bool Partial { get; set; }
    }
}
