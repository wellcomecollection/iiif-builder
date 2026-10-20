using NetVips;

namespace Wellcome.Dds.Server.Images;

/// <summary>
/// Resize an image to a width, preserving aspect ratio, and encode it as JPEG. libvips uses
/// shrink-on-load for JPEG sources and a Lanczos3 kernel, and honours EXIF orientation.
/// </summary>
public static class JpegResizer
{
    public const int DefaultQuality = 80;

    public static byte[] ResizeToWidth(byte[] source, int width, int quality = DefaultQuality)
    {
        // Enums.Size.Both allows upscaling, matching the previous behaviour for widths above the source's.
        using var image = Image.ThumbnailBuffer(source, width, size: Enums.Size.Both);
        return image.JpegsaveBuffer(q: quality);
    }

    public static byte[] ResizeToWidth(string sourcePath, int width, int quality = DefaultQuality)
    {
        using var image = Image.Thumbnail(sourcePath, width, size: Enums.Size.Both);
        return image.JpegsaveBuffer(q: quality);
    }
}
