using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IDigitalObject
    {
        DdsIdentity? Identifier { get; set; }
        bool Partial { get; set; }
        bool? InSyncWithDlcs { get; }
    }
}
