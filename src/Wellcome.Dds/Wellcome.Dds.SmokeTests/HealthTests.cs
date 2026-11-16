using System;
using System.Net;
using System.Web;
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

    [Fact]
    public async Task Dashboard_sends_anonymous_users_to_the_Entra_login()
    {
        // Checks the Identity.Web wiring (authority, and a redirect back to this host), not the login itself
        var response = await Fetch.GetWithoutRedirect("dash/");

        response.Status.Should().Be(HttpStatusCode.Redirect, response.ToString());
        var location = response.Location;
        location.Should().NotBeNull();
        location!.Host.Should().Be("login.microsoftonline.com");
        HttpUtility.ParseQueryString(location.Query)["redirect_uri"]
            .Should().Be(SmokeEnvironment.Url("dash/signin-oidc"));
    }
}
