using System;

namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// This interface is currently populated by Premis sections from METS
    /// Born digital PREMIS and Goobi PREMIS contribute very different data,
    /// so this metadata is used quite differently when building manifestations.
    /// </summary>
    public interface IAssetMetadata
    {
        string GetFileName();
        string GetFolder();
        string GetFileSize();
        string GetFormatName();
        string GetFormatVersion();
        string GetPronomKey();
        string GetAssetId();
        string GetDisplayDuration();
        double GetDuration();
        string GetBitrateKbps();
        int GetNumberOfPages();
        int GetNumberOfImages();
        int GetImageWidth();
        int GetImageHeight();
        
        // Born Digital additions:
        string GetOriginalName();
        string GetMimeType();
        DateTime? GetCreatedDate();

        IRightsStatement GetRightsStatement();

    }
}
