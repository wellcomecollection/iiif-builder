using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    public abstract class BaseDigitalObject : IDigitalObject
    {
        public DdsIdentifier? Identifier { get; set; }
        public bool? InSyncWithDlcs { get; set; }
        public bool Partial { get; set; }
    }
}
