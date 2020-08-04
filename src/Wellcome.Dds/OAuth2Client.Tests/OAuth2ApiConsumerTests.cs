using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OAuth2;
using Test.Helpers;
using Utils.Caching;
using Xunit;

namespace OAuth2Client.Tests
{
    public class OAuth2ApiConsumerTests
    {
        public class ArchiveStorageServiceWorkStorageFactoryTests
        {
            private readonly ControllableHttpMessageHandler httpHandler;
            private readonly OAuth2ApiConsumer sut;
            private readonly ClientCredentials clientCredentials;

            public ArchiveStorageServiceWorkStorageFactoryTests()
            {
                httpHandler = new ControllableHttpMessageHandler();
                var httpClient = new HttpClient(httpHandler);

                clientCredentials = new ClientCredentials
                {
                    ClientId = "super-secret",
                    ClientSecret = "earfquake",
                    TokenEndPoint = "https://token.endpoint/oauth2/token",
                    Scope = "https://api.endpoint/storage/"
                };

                sut = new OAuth2ApiConsumer(httpClient);
            }

            [Fact]
            public void GetToken_Throws_IfUnsuccessfulCallResult()
            {
                // Arrange
                httpHandler.SetResponse(httpHandler.GetResponseMessage(
                    "", HttpStatusCode.Forbidden));

                // Act
                Func<Task> action = () => sut.GetToken(clientCredentials);

                // Assert
                action.Should().Throw<HttpRequestException>();
            }

            [Fact]
            public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_DefaultScope()
            {
                // Arrange
                HttpRequestMessage message = null;
                httpHandler.RegisterCallback(r => message = r);

                var httpResponseMessage = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(httpResponseMessage);


                var expected =
                    "grant_type=client_credentials&client_id=super-secret&client_secret=earfquake&scope=https%3A%2F%2Fapi.endpoint%2Fstorage%2F";

                // Act
                await sut.GetToken(clientCredentials);

                // Assert
                httpHandler.CallsMade.Should().ContainSingle()
                    .Which.Should().Be("https://token.endpoint/oauth2/token");
                message.Method.Should().Be(HttpMethod.Post);
                (await message.Content.ReadAsStringAsync()).Should().Be(expected);
            }

            [Fact]
            public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_SpecifiedScope()
            {
                // Arrange
                HttpRequestMessage message = null;
                httpHandler.RegisterCallback(r => message = r);

                var httpResponseMessage = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(httpResponseMessage);

                var newScopeCredentials = new ClientCredentials()
                {
                    Scope = "new-scope",
                    TokenEndPoint = clientCredentials.TokenEndPoint,
                    ClientId = clientCredentials.ClientId,
                    ClientSecret = clientCredentials.ClientSecret
                };
                var expected =
                    "grant_type=client_credentials&client_id=super-secret&client_secret=earfquake&scope=new-scope";


                // Act
                await sut.GetToken(newScopeCredentials);

                // Assert
                httpHandler.CallsMade.Should().ContainSingle()
                    .Which.Should().Be("https://token.endpoint/oauth2/token");
                message.Method.Should().Be(HttpMethod.Post);
                (await message.Content.ReadAsStringAsync()).Should().Be(expected);
            }

            [Fact]
            public async Task GetToken_ReturnsExpectedToken()
            {
                // Arrange
                var httpResponseMessage = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(httpResponseMessage);

                var expected = new OAuth2Token
                {
                    TokenType = "jwt",
                    ExpiresIn = 1,
                    AccessToken = "abc123"
                };

                // Act
                var token = await sut.GetToken(clientCredentials);

                // Assert
                token.Should().BeEquivalentTo(expected, opts => opts.Excluding(t => t.Acquired));
            }

            [Fact]
            public async Task GetToken_CachesTokenByScope()
            {
                // This is pretty nasty.. Call for scope1..scope2..scope1..scope2
                // verify that we get the correct responses but only 1 calls made to downstream svc

                // Arrange
                var httpResponseMessageOne = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"accessTokenOne\", \"token_type\": \"jwt\", \"expires_in\": 63}", HttpStatusCode.OK);
                httpResponseMessageOne.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var httpResponseMessageTwo = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"accessTokenTwo\", \"token_type\": \"jwt\", \"expires_in\": 63}", HttpStatusCode.OK);
                httpResponseMessageTwo.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                httpHandler.SetResponse(httpResponseMessageOne);

                var scopeOneCredentials = new ClientCredentials()
                {
                    Scope = $"one_{DateTime.UtcNow.Ticks}",
                    TokenEndPoint = clientCredentials.TokenEndPoint,
                    ClientId = clientCredentials.ClientId,
                    ClientSecret = clientCredentials.ClientSecret
                };
                var scopeTwoCredentials = new ClientCredentials()
                {
                    Scope = $"two_{DateTime.UtcNow.Ticks}",
                    TokenEndPoint = clientCredentials.TokenEndPoint,
                    ClientId = clientCredentials.ClientId,
                    ClientSecret = clientCredentials.ClientSecret
                };

                var expectedOne = new OAuth2Token
                {
                    TokenType = "jwt",
                    ExpiresIn = 63,
                    AccessToken = "accessTokenOne"
                };

                var expectedTwo = new OAuth2Token
                {
                    TokenType = "jwt",
                    ExpiresIn = 63,
                    AccessToken = "accessTokenTwo"
                };

                // Act
                var tokenOne = await sut.GetToken(scopeOneCredentials);

                httpHandler.SetResponse(httpResponseMessageTwo);

                var tokenTwo = await sut.GetToken(scopeTwoCredentials);
                var tokenThree = await sut.GetToken(scopeOneCredentials);
                var tokenFour = await sut.GetToken(scopeTwoCredentials);

                // Assert
                tokenOne.Should().Be(tokenThree).And.BeEquivalentTo(expectedOne, opts => opts.Excluding(t => t.Acquired));
                tokenTwo.Should().Be(tokenFour).And.BeEquivalentTo(expectedTwo, opts => opts.Excluding(t => t.Acquired));

                httpHandler.CallsMade.Should().HaveCount(2);
            }
        }
    }
}