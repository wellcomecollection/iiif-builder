namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IAssetMetadata
    {
        string GetFileName();
        string GetFolder();
        string GetFileSize();
        string GetFormatName();
        string GetAssetId();
        string GetLengthInSeconds();
        string GetBitrateKbps();
        int GetNumberOfPages();
        int GetNumberOfImages();
        int GetImageWidth();
        int GetImageHeight();
    }
}
