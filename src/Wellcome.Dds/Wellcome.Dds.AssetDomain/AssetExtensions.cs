using System.Linq;
using IIIF;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain;

public static class AssetExtensions
{        
    public static bool IsVideoMimeType(this string? mimeType)
    {
        return mimeType != null && mimeType.StartsWith("video/");
    }

    public static bool IsAudioMimeType(this string? mimeType)
    {
        return mimeType != null && mimeType.StartsWith("audio/");
    }

    public static bool IsTimeBasedMimeType(this string? mimeType)
    {
        return mimeType.IsVideoMimeType() || mimeType.IsAudioMimeType();
    }
        
    public static bool IsImageMimeType(this string? mimeType)
    {
        return mimeType != null && mimeType.StartsWith("image/");
    }
        
    public static bool IsTextMimeType(this string? mimeType)
    {
        return mimeType != null && mimeType.StartsWith("text/");
    }

    public static IStoredFile? GetDefaultFile(this IPhysicalFile asset)
    {
        return asset.Files!.SingleOrDefault(f => f.StorageIdentifier == asset.StorageIdentifier);
    }

    public static IProcessingBehaviour GetDefaultProcessingBehaviour(this IPhysicalFile asset)
    {
        var defaultStoredFile = asset.GetDefaultFile();
        return defaultStoredFile!.ProcessingBehaviour;
    }
    
    public static Size? GetWhSize(this IPhysicalFile file)
    {
        var dimensions = file.AssetMetadata?.GetMediaDimensions();
        if (dimensions == null) return null;
            
        var w = dimensions.Width.GetValueOrDefault();
        var h = dimensions.Height.GetValueOrDefault();
        if (w > 0 && h > 0)
        {
            return new Size(w, h);
        }

        return null;
    }
    
    
}