namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDigitalObject
    {
        string Identifier { get; set; }
        bool Partial { get; set; }
        bool? InSyncWithDlcs { get; }
    }
}
