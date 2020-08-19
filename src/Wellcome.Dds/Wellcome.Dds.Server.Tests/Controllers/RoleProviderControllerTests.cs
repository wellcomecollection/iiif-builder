using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Wellcome.Dds.Server.Tests.Integration;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Controllers
{
    [Trait("Category","Integration")]
    public class RoleProviderControllerTests : IClassFixture<DdsServerAppFactory>
    {
        private readonly HttpClient client;
        private const string BasicAuth = "ZGxjczpkbGNzcHdvcmQ="; //dlcs:dlcspword

        public RoleProviderControllerTests(DdsServerAppFactory factory)
        {
            client = factory.CreateClient();
        }

        [Fact]
        public async Task RolesForToken_401_IfNoAuth()
        {
            // Arrange
            var requestUri = "/roleprovider/rolesfortoken";
            
            // Act
            var response = await client.GetAsync(requestUri);
            
            // Assert
            response.StatusCode.Should().Be(401);
            var authenticationHeaderValue = response.Headers.WwwAuthenticate.FirstOrDefault();
            
            authenticationHeaderValue.Parameter.Should().Be("realm=\"Wellcome\"");
            authenticationHeaderValue.Scheme.Should().Be("Basic");
        }
        
        [Theory]
        [InlineData("Zm9vOg==", "foo:")]
        [InlineData("OmJhcg==", ":bar")]
        [InlineData("Zm9vOmJhcg==", "foo:bar")]
        public async Task RolesForToken_401_IfInvalidAuth(string headerValue, string clearValue)
        {
            // Arrange
            var requestUri = "/roleprovider/rolesfortoken";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", headerValue);
            
            // Act
            var response = await client.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(401, "unexpected status code for {0}", clearValue);
        }
        
        [Fact]
        public async Task RolesForToken_404_IfTokenNotProvided()
        {
            // Arrange
            var requestUri = "/roleprovider/rolesfortoken";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth);
            
            // Act
            var response = await client.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(404);
        }
    }
}