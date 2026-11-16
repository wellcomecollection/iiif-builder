using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

/// <summary>
/// Images the server renders itself: libvips placeholder canvases and /thumb (AV posters, born-digital PDFs).
/// These show whether the native libraries and pdftoppm made it into the image.
/// </summary>
[Trait("Category", "Smoke")]
public class RenderingTests
{
    private static SmokeFixtures F => SmokeEnvironment.Fixtures;

    [Theory]
    [InlineData("x-fmt/92/image/vnd.adobe.photoshop")]
    [InlineData("fmt/412/application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task Placeholder_canvas_renders(string pronomAndMediaType)
    {
        var response = await Fetch.Get($"extensions/born-digital/placeholder-canvas/{pronomAndMediaType}");

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        ImageHeader.Png(response.Body).Should().Be((1000, 800));
    }

    [SkippableFact]
    public async Task Born_digital_placeholder_canvases_render()
    {
        // Files that aren't images (a .ppt, a .docx) are painted as a placeholder canvas made by libvips
        Skip.If(F.BornDigitalManifest == null, "No born-digital item for this environment");
        var manifest = await PresentationTests.GetJson($"presentation/{F.BornDigitalManifest}");
        var placeholders = manifest.GetProperty("items").EnumerateArray()
            .Select(c => Iiif.Id(Iiif.Body(c)))
            .Where(id => id != null && id.Contains("/placeholder-canvas/"))
            .Take(3).ToList();
        Skip.If(placeholders.Count == 0, $"{F.BornDigitalManifest} has no placeholder canvases");

        foreach (var placeholder in placeholders)
        {
            var response = await Fetch.Get(placeholder!);
            response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
            ImageHeader.Png(response.Body).Should().Be((1000, 800), placeholder);
        }
    }

    [SkippableFact]
    public async Task AV_poster_thumbnail_keeps_its_aspect_ratio()
    {
        Skip.If(F.AvManifest == null, "No AV item for this environment");
        var full = await Fetch.Get($"thumb/{F.AvManifest}");
        var small = await Fetch.Get($"thumb/{F.AvManifest}/300");

        full.Status.Should().Be(HttpStatusCode.OK, full.ToString());
        small.Status.Should().Be(HttpStatusCode.OK, small.ToString());
        var (fullWidth, fullHeight) = ImageHeader.Jpeg(full.Body)!.Value;
        var (width, height) = ImageHeader.Jpeg(small.Body)!.Value;
        width.Should().Be(300);
        height.Should().BeCloseTo(300 * fullHeight / fullWidth, 1, "the poster is scaled, not squared");
    }

    [SkippableFact]
    public async Task PDF_thumbnail_renders_at_requested_widths()
    {
        Skip.If(F.PdfThumbnailItem == null, "No born-digital PDF for this environment");
        var standard = await Fetch.Get($"thumb/{F.PdfThumbnailItem}");
        var wider = await Fetch.Get($"thumb/{F.PdfThumbnailItem}/400");

        standard.Status.Should().Be(HttpStatusCode.OK, standard.ToString());
        ImageHeader.Jpeg(standard.Body)!.Value.Width.Should().Be(200);
        wider.Status.Should().Be(HttpStatusCode.OK, wider.ToString());
        ImageHeader.Jpeg(wider.Body)!.Value.Width.Should().Be(400);
    }

    [SkippableFact]
    public async Task PDF_thumbnail_refuses_widths_over_the_maximum()
    {
        Skip.If(F.PdfThumbnailItem == null, "No born-digital PDF for this environment");
        var response = await Fetch.Get($"thumb/{F.PdfThumbnailItem}/2000");

        response.Status.Should().Be(HttpStatusCode.BadRequest, response.ToString());
    }
}
