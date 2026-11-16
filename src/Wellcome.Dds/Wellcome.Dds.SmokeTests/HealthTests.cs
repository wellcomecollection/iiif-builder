using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.SmokeTests;

[Trait("Category", "Smoke")]
public class HealthTests
{
    [Theory]
    [InlineData("management/healthcheck")]
    [InlineData("dash/management/healthcheck")]
    public async Task Service_reports_healthy(string path)
    {
        // The dashboard's check includes both databases
        var response = await Fetch.Get(path);

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        response.Text.Should().Be("Healthy");
    }

    [Fact]
    public async Task Swagger_loads()
    {
        var response = await Fetch.Get("swagger/index.html");

        response.Status.Should().Be(HttpStatusCode.OK, response.ToString());
        response.ContentType.Should().Be("text/html");
    }
}
