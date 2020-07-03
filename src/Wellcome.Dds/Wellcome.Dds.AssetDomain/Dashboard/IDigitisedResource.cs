namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDigitisedResource
    {
        BNumberModel BNumberModel { get; set; }
        string Identifier { get; set; }
        bool Partial { get; set; }
        bool? InSyncWithDlcs { get; }
    }
}
