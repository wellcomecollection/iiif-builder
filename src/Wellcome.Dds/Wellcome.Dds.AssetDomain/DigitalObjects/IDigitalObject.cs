namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IDigitalObject
    {
        string Identifier { get; set; }
        bool Partial { get; set; }
        bool? InSyncWithDlcs { get; }
    }
}
