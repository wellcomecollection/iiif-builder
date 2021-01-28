using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Wellcome.Dds.Server.Conneg;
using Wellcome.Dds.Server.Tests.Integration;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Controllers
{
    // NOTE - marked as manual for now - can deal with catalogue API + s3 dependencies when fully implemented
    [Trait("Category", "Integration")]
    [Trait("Category", "Manual")]
    public class PresentationControllerTests : IClassFixture<DdsServerAppFactory>
    {
        private readonly HttpClient client;

        public PresentationControllerTests(DdsServerAppFactory factory)
        {
            client = factory.CreateClient();
        }

        [Fact]
        public async Task Presentation_ReturnsV3_IfNoSpecificVersionRequested()
        {
            // Arrange
            var requestUri = "/presentation/b16235083";

            // Act
            var response = await client.GetAsync(requestUri);

            // Assert
            var contentType = response.Content.Headers.ContentType;
            contentType.MediaType.Should().Be("application/ld+json");
            contentType.Parameters.Single(p => p.Name == "profile").Value.Should()
                .Be("\"http://iiif.io/api/presentation/3/context.json\"");
        }

        [Fact]
        public async Task Presentation_ReturnsV2_FromV2Endpoint()
        {
            // Arrange
            var requestUri = "/presentation/v2/b29718697";

            // Act
            var response = await client.GetAsync(requestUri);

            // Assert
            var contentType = response.Content.Headers.ContentType;
            contentType.MediaType.Should().Be("application/ld+json");
            contentType.Parameters.Single(p => p.Name == "profile").Value.Should()
                .Be("\"http://iiif.io/api/presentation/2/context.json\"");
        }

        [Fact]
        public async Task Presentation_ReturnsV3_FromV3Endpoint()
        {
            // Arrange
            var requestUri = "/presentation/v3/b16235083";

            // Act
            var response = await client.GetAsync(requestUri);

            // Assert
            var contentType = response.Content.Headers.ContentType;
            contentType.MediaType.Should().Be("application/ld+json");
            contentType.Parameters.Single(p => p.Name == "profile").Value.Should()
                .Be("\"http://iiif.io/api/presentation/3/context.json\"");
        }

        [Theory]
        [InlineData(IIIFPresentation.ContentTypes.V3, "\"http://iiif.io/api/presentation/3/context.json\"")]
        [InlineData(IIIFPresentation.ContentTypes.V2, "\"http://iiif.io/api/presentation/2/context.json\"")]
        public async Task Presentation_ReturnsSpecifiedVersion_UsingAcceptsHeader(string accepts,
            string expectedContentType)
        {
            // Arrange
            var requestUri = "/presentation/b16235083";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(accepts));

            // Act
            var response = await client.SendAsync(request);

            // Assert
            var contentType = response.Content.Headers.ContentType;
            contentType.MediaType.Should().Be("application/ld+json");
            contentType.Parameters.Single(p => p.Name == "profile").Value.Should()
                .Be(expectedContentType);
        }
    }
}