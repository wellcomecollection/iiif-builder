namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDigitisedResource
    {
        string Identifier { get; set; }
        bool Partial { get; set; }
        bool? InSyncWithDlcs { get; }
    }
}
