using System.Linq;
using Utils;

namespace Wellcome.Dds.AssetDomain.Dlcs
{
    public enum AssetFamily
    {
        Image = 'I',
        TimeBased = 'T',
        File = 'F'
    }

    public static class AssetFamilyUtils
    {
        public static AssetFamily GetAssetFamily(this string? mediaType, string[]? permittedImages = null)
        {
            if (mediaType.IsImageMimeType())
            {
                if (!permittedImages.HasItems()) return AssetFamily.Image;
                var part2 = mediaType.RemoveStart("image/");
                if (permittedImages.Contains(part2))
                {
                    return AssetFamily.Image;
                }

                return AssetFamily.File;
            }
            if (mediaType.IsTimeBasedMimeType())
            {
                return AssetFamily.TimeBased;
            }
            return AssetFamily.File;
        }
        
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
    }
    
}
