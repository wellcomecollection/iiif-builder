using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;
using Wellcome.Dds.Server.Pdf;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Pdf;

public class CoverPageLogoCacheTests
{
    private const string Url = "https://example.org/logo.png";

    private readonly List<string> requests = new();
    private readonly List<string?> userAgents = new();
    private Func<HttpResponseMessage> respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);

    private CoverPageLogoCache Sut(IFileProvider? webRoot = null)
    {
        var handler = new StubHandler(request =>
        {
            requests.Add(request.RequestUri!.ToString());
            userAgents.Add(request.Headers.UserAgent.ToString());
            return respond();
        });
        var environment = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => environment.WebRootFileProvider).Returns(webRoot ?? new NullFileProvider());
        return new CoverPageLogoCache(new StubFactory(handler), environment, NullLogger<CoverPageLogoCache>.Instance);
    }

    [Fact]
    public async Task A_Logo_This_Server_Serves_Itself_Is_Read_From_The_Web_Root_Not_Fetched()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "cover-page-logo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(webRoot, "logos"));
        try
        {
            var png = Png();
            await File.WriteAllBytesAsync(Path.Combine(webRoot, "logos", "wellcome-collection-black.png"), png);
            var sut = Sut(new PhysicalFileProvider(webRoot));

            var logo = await sut.Get("https://iiif.wellcomecollection.org/logos/wellcome-collection-black.png");

            logo.Should().Equal(png);
            requests.Should().BeEmpty("the file is on disk, so there is no reason to go through the edge and back");
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static byte[] Png()
    {
        using var image = (Image.Black(200, 66, bands: 3) + 255).Cast(Enums.BandFormat.Uchar);
        return image.PngsaveBuffer();
    }

    [Fact]
    public async Task A_Png_Is_Fetched_Once_And_Served_From_The_Cache()
    {
        var png = Png();
        respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
        var sut = Sut();

        var first = await sut.Get(Url);
        var second = await sut.Get(Url);

        first.Should().Equal(png);
        second.Should().BeSameAs(first);
        requests.Should().ContainSingle();
        // the logo's CloudFront rejects requests with no User-Agent, which HttpClient sends by default
        userAgents.Should().ContainSingle().Which.Should().Be(CoverPageLogoCache.UserAgent);
    }

    [Fact]
    public async Task Something_That_Is_Not_An_Image_Is_Not_Used_And_Not_Refetched_For_A_While()
    {
        respond = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Sorry, something went wrong</body></html>", Encoding.UTF8, "text/html")
        };
        var sut = Sut();

        (await sut.Get(Url)).Should().BeNull();
        (await sut.Get(Url)).Should().BeNull();

        requests.Should().ContainSingle("a bad answer is remembered, not retried on every cover page");
    }

    [Fact]
    public async Task A_Failed_Fetch_Means_No_Logo_Not_An_Error()
    {
        respond = () => throw new HttpRequestException("connection refused");
        var sut = Sut();

        (await sut.Get(Url)).Should().BeNull();
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler handler;
        public StubFactory(HttpMessageHandler handler) => this.handler = handler;
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
