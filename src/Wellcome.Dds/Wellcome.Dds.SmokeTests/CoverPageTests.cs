using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using UglyToad.PdfPig;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

/// <summary>
/// The server's PDF cover page (PdfPig). It answers at /pdf-cover-v2 everywhere; /pdf-cover reaches it only
/// once the edge routes that path to the server, so that path isn't checked here.
/// </summary>
[Trait("Category", "Smoke")]
public class CoverPageTests
{
    private static SmokeFixtures F => SmokeEnvironment.Fixtures;

    [SkippableFact]
    public async Task Cover_page_is_one_page_with_embedded_Inter()
    {
        Skip.If(F.CoverPageItem == null, "No cover page item for this environment");
        var response = await Fetch.Get($"pdf-cover-v2/{F.CoverPageItem}");

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        response.ContentType.Should().Be("application/pdf");
        using var pdf = PdfDocument.Open(response.Body);
        pdf.NumberOfPages.Should().Be(1);
        pdf.Information.Producer.Should().Be("PdfPig");
        var page = pdf.GetPage(1);
        page.Letters.Select(l => l.FontName).Distinct()
            .Should().Contain(f => f.Contains("Inter"), "the Inter fonts are embedded");
        page.Text.Should().Contain("wellcomecollection.org");
    }

    [Fact]
    public async Task Cover_page_ping_answers()
    {
        var response = await Fetch.Get("pdf-cover-v2/ping");

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
    }

    [Fact]
    public async Task Cover_page_for_unknown_item_is_404()
    {
        var response = await Fetch.Get("pdf-cover-v2/b00000000");

        response.Status.Should().Be(HttpStatusCode.NotFound, response.ToString());
    }
}
