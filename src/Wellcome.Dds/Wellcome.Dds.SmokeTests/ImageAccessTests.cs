using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

/// <summary>
/// What anonymous users can get from the DLCS image services for each kind of item. These are the
/// behaviours the Protagonist 1.15 upgrade and the DDS's switch to maxWidth/openFullMax must preserve
/// (see docs/smoke-test.md).
/// </summary>
[Trait("Category", "Smoke")]
public class ImageAccessTests
{
    private static SmokeFixtures F => SmokeEnvironment.Fixtures;

    [SkippableFact]
    public async Task Open_item_has_no_size_limit_and_serves_tiles()
    {
        Skip.If(F.OpenImageItem == null, "No open image item for this environment");
        var manifest = await PresentationTests.GetJson($"presentation/{F.OpenImageItem}");

        foreach (var canvas in Iiif.SampleCanvases(manifest))
        {
            var service = Iiif.ImageServiceId(canvas);
            service.Should().NotBeNull($"canvas {Iiif.Id(canvas)} has an image service");

            var info = await Fetch.Get($"{service}/info.json");
            info.Status.Should().Be(HttpStatusCode.OK, info.ToString());
            Iiif.MaxWidth(info.Json).Should().BeNull($"{service} is open with no size limit");

            var tile = await Fetch.Get(Iiif.TileUrl(service!));
            tile.Status.Should().Be(HttpStatusCode.OK, tile.ToString());
            ImageHeader.Jpeg(tile.Body).Should().NotBeNull("the tile is a JPEG");
        }
    }

    [SkippableFact]
    public async Task Size_limited_item_serves_1000px_but_no_tiles()
    {
        Skip.If(F.SizeLimitedItem == null, "No size-limited item for this environment");
        var manifest = await PresentationTests.GetJson($"presentation/{F.SizeLimitedItem}");
        var service = Iiif.ImageServiceId(manifest.GetProperty("items")[0]);
        service.Should().NotBeNull();

        // The DLCS answers 401 with a limited info.json
        var info = await Fetch.Get($"{service}/info.json");
        Iiif.MaxWidth(info.Json).Should().Be(1000, info.ToString());
        Iiif.ServiceTypes(info.Json).Should().NotContain(t => t.Contains("Auth"), "there is no login to offer");

        var fits = await Fetch.Get($"{service}/full/!1000,1000/0/default.jpg");
        fits.Status.Should().Be(HttpStatusCode.OK, fits.ToString());

        var tile = await Fetch.Get(Iiif.TileUrl(service!));
        tile.Status.Should().Be(HttpStatusCode.Unauthorized, tile.ToString());
    }

    [SkippableFact]
    public async Task Access_controlled_item_offers_auth_and_refuses_anonymous_tiles()
    {
        Skip.If(F.AuthItem == null, "No access-controlled item for this environment");
        var manifest = await PresentationTests.GetJson($"presentation/{F.AuthItem}");
        var service = Iiif.ImageServiceId(manifest.GetProperty("items")[0]);
        service.Should().NotBeNull();

        var info = await Fetch.Get($"{service}/info.json");
        info.Status.Should().Be(HttpStatusCode.Unauthorized, info.ToString());
        Iiif.ServiceTypes(info.Json).Should().Contain(t => t.Contains("Auth"), "the info.json offers a login");

        var tile = await Fetch.Get(Iiif.TileUrl(service!));
        tile.Status.Should().Be(HttpStatusCode.Unauthorized, tile.ToString());

        // IIIF Auth 2: the probe says 401 to an anonymous user, and the access service offers its page
        var probe = Iiif.FindService(info.Json, "AuthProbeService2");
        probe.Should().NotBeNull("the info.json offers an Auth 2 probe service");
        var probeResult = await Fetch.Get(Iiif.Id(probe!.Value)!);
        probeResult.Status.Should().Be(HttpStatusCode.OK, probeResult.ToString());
        Iiif.Type(probeResult.Json).Should().Be("AuthProbeResult2");
        probeResult.Json.GetProperty("status").GetInt32().Should().Be(401);

        var access = Iiif.FindService(probe.Value, "AuthAccessService2");
        access.Should().NotBeNull("the probe service has an access service");
        var accessPage = await Fetch.Get($"{Iiif.Id(access!.Value)}?origin=https://example.org");
        accessPage.Status.Should().Be(HttpStatusCode.OK, accessPage.ToString());
        accessPage.ContentType.Should().Be("text/html");
    }

    [SkippableFact]
    public async Task Thumbnails_are_served_for_every_kind_of_item()
    {
        // Thumbnails stay open even where the full image is size-limited or behind a login
        var items = new[] { F.OpenImageItem, F.SizeLimitedItem, F.AuthItem }.OfType<string>().Distinct().ToList();
        Skip.If(items.Count == 0, "No image items for this environment");

        foreach (var item in items)
        {
            var manifest = await PresentationTests.GetJson($"presentation/{item}");
            var thumbnails = new List<string?> { Iiif.ThumbnailId(manifest), Iiif.ThumbnailId(manifest.GetProperty("items")[0]) };
            foreach (var thumbnail in thumbnails)
            {
                thumbnail.Should().NotBeNull($"{item} and its first canvas have thumbnails");
                var response = await Fetch.Get(thumbnail!);
                response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
                ImageHeader.Jpeg(response.Body).Should().NotBeNull($"{thumbnail} is a JPEG");
            }
        }
    }
}
