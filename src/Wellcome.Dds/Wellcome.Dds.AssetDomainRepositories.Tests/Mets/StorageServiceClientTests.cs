using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OAuth2;
using Test.Helpers;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class StorageServiceClientTests
    {
        private const string TokenEndPoint = "https://token.endpoint/oauth2/token";

        [Theory]
        [InlineData(HttpStatusCode.NotFound, LogLevel.Warning)]
        [InlineData(HttpStatusCode.InternalServerError, LogLevel.Error)]
        public async Task LoadStorageManifest_LogsMissingBagAsWarning_OtherFailuresAsError(
            HttpStatusCode status, LogLevel expectedLevel)
        {
            // Arrange
            var httpHandler = new ControllableHttpMessageHandler();
            var tokenResponse = httpHandler.GetResponseMessage(
                "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 3600}", HttpStatusCode.OK);
            tokenResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.RegisterCallback(r => httpHandler.SetResponse(
                r.RequestUri!.ToString() == TokenEndPoint ? tokenResponse : new HttpResponseMessage(status)));

            var logger = new CapturingLogger<StorageServiceClient>();
            var sut = new StorageServiceClient(
                new OAuth2ApiConsumer(new HttpClient(httpHandler), new NullLogger<OAuth2ApiConsumer>()),
                Options.Create(new StorageOptions
                {
                    StorageApiTemplate = "https://storage.example/{0}/{1}",
                    TokenEndPoint = TokenEndPoint,
                    // OAuth2ApiConsumer caches tokens by scope across instances
                    Scope = $"{nameof(StorageServiceClientTests)}_{status}_{DateTime.UtcNow.Ticks}",
                    ClientId = "id",
                    ClientSecret = "secret"
                }),
                logger);

            // Act
            Func<Task> action = () => sut.LoadStorageManifest("born-digital", "zzzzzzzz");

            // Assert
            await action.Should().ThrowAsync<HttpRequestException>();
            logger.Entries.Where(e => e.Level >= LogLevel.Warning)
                .Should().ContainSingle()
                .Which.Level.Should().Be(expectedLevel);
        }
    }
}
