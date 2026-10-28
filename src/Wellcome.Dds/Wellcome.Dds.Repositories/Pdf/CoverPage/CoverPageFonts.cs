using System;
using System.Collections.Concurrent;
using System.IO;
using UglyToad.PdfPig.Fonts.TrueType;
using UglyToad.PdfPig.Fonts.TrueType.Parser;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// A TrueType font the cover page is set in: its bytes, for embedding, and its character map, for
/// knowing which characters it can show before asking PdfPig to draw them (PdfPig throws for a
/// character the font has no glyph for).
/// </summary>
public class CoverPageFont
{
    private readonly TrueTypeFont font;
    private readonly ConcurrentDictionary<char, bool> known = new();

    public CoverPageFont(byte[] bytes)
    {
        Bytes = bytes;
        font = TrueTypeFontParser.Parse(new TrueTypeDataBytes(bytes));
    }

    public byte[] Bytes { get; }
    public string Name => font.Name;

    /// <summary>
    /// Whether the font has a glyph for this character.
    /// </summary>
    public bool Has(char c)
    {
        return known.GetOrAdd(c, ch => font.TryGetBoundingBox(ch, out _));
    }
}

/// <summary>
/// Inter, embedded in this assembly. The retired PdfCoverPage service shipped the same files because
/// the PDF standard fonts could not render all the characters that appear in catalogue titles.
/// </summary>
public static class CoverPageFonts
{
    private static readonly Lazy<CoverPageFont> regular = new(() => new CoverPageFont(Load("Inter-Regular.ttf")));
    private static readonly Lazy<CoverPageFont> bold = new(() => new CoverPageFont(Load("Inter-Bold.ttf")));

    public static CoverPageFont Regular => regular.Value;
    public static CoverPageFont Bold => bold.Value;

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
