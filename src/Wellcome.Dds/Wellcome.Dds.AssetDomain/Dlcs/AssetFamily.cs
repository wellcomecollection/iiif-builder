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
        public static AssetFamily GetAssetFamily(this string mediaType)
        {
            if (mediaType.StartsWith("image/"))
            {
                return AssetFamily.Image;
            }
            if (mediaType.StartsWith("video/") || mediaType.StartsWith("audio/"))
            {
                return AssetFamily.TimeBased;
            }
            return AssetFamily.File;
        }
    }
}
