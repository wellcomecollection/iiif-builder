using System;
using System.IO;
using System.Reflection;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// Inter, embedded in this assembly. The retired PdfCoverPage service shipped the same files because
/// the PDF standard fonts could not render all the characters that appear in catalogue titles.
/// </summary>
public static class CoverPageFonts
{
    private static readonly Lazy<byte[]> regular = new(() => Load("Inter-Regular.ttf"));
    private static readonly Lazy<byte[]> bold = new(() => Load("Inter-Bold.ttf"));

    public static byte[] Regular => regular.Value;
    public static byte[] Bold => bold.Value;

    private static byte[] Load(string fileName)
    {
        var assembly = typeof(CoverPageFonts).Assembly;
        var resourceName = $"Wellcome.Dds.Repositories.Resources.fonts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font {resourceName} is missing");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
