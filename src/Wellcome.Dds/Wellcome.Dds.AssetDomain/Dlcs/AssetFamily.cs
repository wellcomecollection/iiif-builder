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

    }
    
}
