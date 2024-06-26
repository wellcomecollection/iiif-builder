﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OAuth2;
using Test.Helpers;
using Xunit;

namespace OAuth2Client.Tests
{
    public class OAuth2ApiConsumerTests
    {
        public class ArchiveStorageServiceWorkStorageFactoryTests
        {
            private readonly ControllableHttpMessageHandler httpHandler;
            private readonly OAuth2ApiConsumer sut;

            public ArchiveStorageServiceWorkStorageFactoryTests()
            {
                httpHandler = new ControllableHttpMessageHandler();
                var httpClient = new HttpClient(httpHandler);

                sut = new OAuth2ApiConsumer(httpClient, new NullLogger<OAuth2ApiConsumer>());
            }

            [Fact]
            public async Task GetToken_Throws_IfUnsuccessfulCallResult()
            {
                // Arrange
                httpHandler.SetResponse(httpHandler.GetResponseMessage(
                    "", HttpStatusCode.Forbidden));

                // Act
                var clientCredentials = GetClientCredentials(nameof(GetToken_Throws_IfUnsuccessfulCallResult));
                Func<Task> action = () => sut.GetToken(clientCredentials);

                // Assert
                await action.Should().ThrowAsync<HttpRequestException>();
            }

            [Fact]
            public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_CredentialsInContent()
            {
                // Arrange
                HttpRequestMessage message = null;
                httpHandler.RegisterCallback(r => message = r);

                var httpResponseMessage = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(httpResponseMessage);

                var expected =
                    "grant_type=client_credentials&scope=GetToken_CallsTokenEndpoint_WithCorrectObject_CredentialsInContent&client_id=super-secret&client_secret=earfquake";

                // Act
                var clientCredentials = GetClientCredentials(nameof(GetToken_CallsTokenEndpoint_WithCorrectObject_CredentialsInContent));
                await sut.GetToken(clientCredentials, true);

                // Assert
                httpHandler.CallsMade.Should().ContainSingle()
                    .Which.Should().Be("https://token.endpoint/oauth2/token");
                message.Method.Should().Be(HttpMethod.Post);
                (await message.Content.ReadAsStringAsync()).Should().Be(expected);
            }

            [Fact]
            public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_AndBasicAuth()
            {
                // Arrange
                HttpRequestMessage message = null;
                httpHandler.RegisterCallback(r => message = r);

                var httpResponseMessage = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(httpResponseMessage);

                var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes("super-secret:earfquake")); 
                var expected =
                    "grant_type=client_credentials&scope=GetToken_CallsTokenEndpoint_WithCorrectObject_AndBasicAuth";
                
                // Act
                var clientCredentials =
                    GetClientCredentials(nameof(GetToken_CallsTokenEndpoint_WithCorrectObject_AndBasicAuth));
                await sut.GetToken(clientCredentials);

                // Assert
                httpHandler.CallsMade.Should().ContainSingle()
                    .Which.Should().Be("https://token.endpoint/oauth2/token");
                message.Method.Should().Be(HttpMethod.Post);
                (await message.Content.ReadAsStringAsync()).Should().Be(expected);
                message.Headers.Authorization.Scheme.Should().Be("Basic");
                message.Headers.Authorization.Parameter.Should().Be(basicAuth);
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
                var clientCredentials =
                    GetClientCredentials(nameof(GetToken_ReturnsExpectedToken));
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

                var clientCredentials =
                    GetClientCredentials(nameof(GetToken_CachesTokenByScope));
                
                var scopeOneCredentials = new ClientCredentials
                {
                    Scope = $"one_{DateTime.UtcNow.Ticks}",
                    TokenEndPoint = clientCredentials.TokenEndPoint,
                    ClientId = clientCredentials.ClientId,
                    ClientSecret = clientCredentials.ClientSecret
                };
                var scopeTwoCredentials = new ClientCredentials
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

            [Fact]
            public async Task GetToken_ForcesNewToken_IfForceNewTokenTrue()
            {
                // This is pretty nasty.. Call for scope1..scope2..scope1..scope2
                // verify that we get the correct responses but only 1 calls made to downstream svc

                // Arrange
                var response = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"accessTokenOne\", \"token_type\": \"jwt\", \"expires_in\": 63}",
                    HttpStatusCode.OK);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                httpHandler.SetResponse(response);

                var clientCredentials =
                    GetClientCredentials(nameof(GetToken_ForcesNewToken_IfForceNewTokenTrue));
                var credentials = new ClientCredentials
                {
                    Scope = $"one_{DateTime.UtcNow.Ticks}",
                    TokenEndPoint = clientCredentials.TokenEndPoint,
                    ClientId = clientCredentials.ClientId,
                    ClientSecret = clientCredentials.ClientSecret
                };

                // Act
                await sut.GetToken(credentials);
                await sut.GetToken(credentials, forceNewToken: true);

                // Assert

                httpHandler.CallsMade.Should().HaveCount(2);
            }

            [Fact]
            public async Task GetOAuthedJToken_Throws_CallFailsForNon403Reason()
            {
                // Arrange
                var tokenResponse = httpHandler.GetResponseMessage(
                    "{\"access_token\": \"accessTokenOne\", \"token_type\": \"jwt\", \"expires_in\": 63}",
                    HttpStatusCode.OK);
                tokenResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                
                var clientCredentials =
                    GetClientCredentials(nameof(GetOAuthedJToken_Throws_CallFailsForNon403Reason));
                httpHandler.RegisterCallback(c =>
                {
                    if (c.RequestUri.ToString() == clientCredentials.TokenEndPoint)
                    {
                        httpHandler.SetResponse(tokenResponse);
                    }
                    else
                    {
                        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                    }
                });
                
                // Act
                Func<Task> action = () => sut.GetOAuthedJToken("http://example.org", clientCredentials);
                
                // Assert
                await action.Should().ThrowAsync<HttpRequestException>();
            }
            
            [Fact]
            public async Task GetOAuthedJToken_RenewsAccessToken_IfCallIs403Response()
            {
                // Arrange
                HttpResponseMessage GetTokenResponse()
                {
                    var httpResponseMessage = httpHandler.GetResponseMessage(
                        "{\"access_token\": \"accessTokenOne\", \"token_type\": \"jwt\", \"expires_in\": 63}",
                        HttpStatusCode.OK);
                    httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    return httpResponseMessage;
                }
                
                var clientCredentials =
                    GetClientCredentials(nameof(GetOAuthedJToken_RenewsAccessToken_IfCallIs403Response));
                const string response = "{\"foo\": \"bar\"";
                var finalResponse = httpHandler.GetResponseMessage(response, HttpStatusCode.OK);
                finalResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                int callCount = 0;
                httpHandler.RegisterCallback(c =>
                {
                    // Return Token
                    if (c.RequestUri.ToString() == clientCredentials.TokenEndPoint)
                    {
                        httpHandler.SetResponse(GetTokenResponse());
                    }
                    else
                    {
                        if (callCount++ == 0)
                        {
                            httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
                        }
                        else
                        {
                            // return token response 
                            httpHandler.SetResponse(GetTokenResponse());
                        }
                    }
                });

                var expectedCalls = new List<string>
                {
                    clientCredentials.TokenEndPoint,
                    "http://example.org/",
                    clientCredentials.TokenEndPoint,
                    "http://example.org/",
                };
                
                // Act
                await sut.GetOAuthedJToken("http://example.org/", clientCredentials);
                
                // Assert
                httpHandler.CallsMade.Should().ContainInOrder(expectedCalls);
            }

            private ClientCredentials GetClientCredentials(string scope)
                => new ClientCredentials
                {
                    ClientId = "super-secret",
                    ClientSecret = "earfquake",
                    TokenEndPoint = "https://token.endpoint/oauth2/token",
                    Scope = scope
                };
        }
    }
}