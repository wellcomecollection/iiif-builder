using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

[Trait("Category", "Smoke")]
public class TextTests
{
    private static SmokeFixtures F => SmokeEnvironment.Fixtures;

    [SkippableFact]
    public async Task ALTO_resolves_for_sampled_canvases()
    {
        Skip.If(F.GoobiManifest == null, "No Goobi item for this environment");
        var manifest = await PresentationTests.GetJson($"presentation/{F.GoobiManifest}");

        foreach (var canvas in Iiif.SampleCanvases(manifest))
        {
            canvas.TryGetProperty("seeAlso", out var seeAlso).Should().BeTrue($"canvas {Iiif.Id(canvas)} links its ALTO");
            var alto = seeAlso.EnumerateArray()
                .FirstOrDefault(s => s.TryGetProperty("profile", out var p) && p.GetString()!.Contains("alto"));
            alto.ValueKind.Should().Be(JsonValueKind.Object, $"canvas {Iiif.Id(canvas)} has an ALTO seeAlso");

            var response = await Fetch.Get(Iiif.Id(alto)!);
            response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
            response.Text.Should().Contain("<alto", $"{Iiif.Id(alto)} is ALTO XML");
        }
    }

    [SkippableFact]
    public async Task Search_within_finds_hits()
    {
        Skip.If(F.SearchItem == null || F.SearchTerm == null, "No search fixture for this environment");
        var response = await Fetch.Get($"search/v1/{F.SearchItem}?q={F.SearchTerm}");

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        response.Json.GetProperty("resources").GetArrayLength().Should().BePositive();
    }
}
