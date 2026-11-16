using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

[Trait("Category", "Smoke")]
public class PresentationTests
{
    private static SmokeFixtures F => SmokeEnvironment.Fixtures;

    [SkippableFact]
    public async Task Goobi_manifest_v3()
    {
        Skip.If(F.GoobiManifest == null, "No Goobi item for this environment");
        var manifest = await GetJson($"presentation/{F.GoobiManifest}");

        Iiif.Type(manifest).Should().Be("Manifest");
        manifest.GetProperty("@context").ToString().Should().Contain("presentation/3");
        manifest.GetProperty("items").GetArrayLength().Should().BePositive();
    }

    [SkippableFact]
    public async Task Goobi_manifest_v2()
    {
        Skip.If(F.GoobiManifest == null, "No Goobi item for this environment");
        var manifest = await GetJson($"presentation/v2/{F.GoobiManifest}");

        Iiif.Type(manifest).Should().Be("sc:Manifest");
        manifest.GetProperty("sequences")[0].GetProperty("canvases").GetArrayLength().Should().BePositive();
    }

    [SkippableFact]
    public async Task Collection_leads_to_manifests()
    {
        // Two levels for a multi-volume work; three for Chemist & Druggist (title, volume, issue)
        Skip.If(F.Collection == null, "No collection for this environment");
        var current = await GetJson($"presentation/{F.Collection}");
        Iiif.Type(current).Should().Be("Collection");

        var path = F.Collection;
        for (var depth = 0; depth < 3 && Iiif.Type(current) == "Collection"; depth++)
        {
            current.GetProperty("items").GetArrayLength().Should().BePositive($"{path} has items");
            path = Iiif.Id(current.GetProperty("items")[0])!;
            current = await GetJson(path);
        }

        Iiif.Type(current).Should().Be("Manifest", $"the first item of each level leads to a Manifest ({path})");
    }

    [Theory]
    [InlineData("presentation/collections", "Collection")]
    [InlineData("presentation/v2/collections", "sc:Collection")]
    [InlineData("presentation/collections/archives", "Collection")]
    public async Task Top_level_collections_list_items(string path, string type)
    {
        var collection = await GetJson(path);

        Iiif.Type(collection).Should().Be(type);
        var items = collection.TryGetProperty("items", out var v3) ? v3
            : collection.TryGetProperty("collections", out var v2) ? v2
            : collection.GetProperty("members");
        items.GetArrayLength().Should().BePositive($"{path} lists something");
    }

    [SkippableFact]
    public async Task Born_digital_manifest()
    {
        Skip.If(F.BornDigitalManifest == null, "No born-digital item for this environment");
        var manifest = await GetJson($"presentation/{F.BornDigitalManifest}");

        Iiif.Type(manifest).Should().Be("Manifest");
        manifest.GetProperty("items").GetArrayLength().Should().BePositive();
    }

    [SkippableFact]
    public async Task AV_manifest_has_audio_or_video()
    {
        Skip.If(F.AvManifest == null, "No AV item for this environment");
        var manifest = await GetJson($"presentation/{F.AvManifest}");

        Iiif.Type(manifest).Should().Be("Manifest");
        var body = Iiif.Body(manifest.GetProperty("items")[0]);
        var types = Iiif.Type(body) == "Choice"
            ? body.GetProperty("items").EnumerateArray().Select(Iiif.Type).ToList()
            : [Iiif.Type(body)];
        types.Should().Contain(t => t == "Video" || t == "Sound");
    }

    internal static async Task<JsonElement> GetJson(string path)
    {
        var response = await Fetch.Get(path);
        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        return response.Json;
    }
}
