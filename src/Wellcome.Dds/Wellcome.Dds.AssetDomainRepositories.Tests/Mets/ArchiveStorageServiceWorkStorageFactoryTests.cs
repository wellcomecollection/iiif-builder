using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Amazon.S3;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers;
using Utils.Aws.S3;
using Utils.Caching;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class ArchiveStorageServiceWorkStorageFactoryTests
    {
        private readonly ControllableHttpMessageHandler httpHandler;
        private readonly IAmazonS3 storageServiceS3;
        private readonly ISimpleCache cache;
        private readonly IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        private readonly ArchiveStorageServiceWorkStorageFactory sut;

        public ArchiveStorageServiceWorkStorageFactoryTests()
        {
            httpHandler = new ControllableHttpMessageHandler();
            var httpClient = new HttpClient(httpHandler);

            cache = A.Fake<ISimpleCache>();
            storageServiceS3 = A.Fake<IAmazonS3>();
            storageMapCache = A.Fake<IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap>>();
            
            var dlcsOptions = new StorageOptions
            {
                ClientId = "super-secret",
                ClientSecret = "earfquake",
                TokenEndPoint = "https://token.endpoint/oauth2/token",
                Scope = "https://api.endpoint/storage/"
            };
            var options = Options.Create(dlcsOptions);

            var storageFactory = A.Fake<INamedAmazonS3ClientFactory>();
            A.CallTo(() => storageFactory.Get(A<string>._)).Returns(storageServiceS3);

            sut = new ArchiveStorageServiceWorkStorageFactory(new NullLogger<ArchiveStorageServiceWorkStorageFactory>(),
                options, storageMapCache, cache, storageFactory, httpClient);
        }
        
        [Fact]
        public void GetToken_Throws_IfUnsuccessfulCallResult()
        {
            // Assert
            httpHandler.SetResponse(httpHandler.GetResponseMessage(
                "", HttpStatusCode.Forbidden));
            
            // Act
            Func<Task> action = () => sut.GetToken();
            
            // Assert
            action.Should().Throw<HttpRequestException>();
        }

        [Fact]
        public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_DefaultScope()
        {
            // Assert
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            var httpResponseMessage = httpHandler.GetResponseMessage(
                "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.SetResponse(httpResponseMessage);


            var expected =
                "grant_type=client_credentials&client_id=super-secret&client_secret=earfquake&scope=https%3A%2F%2Fapi.endpoint%2Fstorage%2F";
            
            // Act
            await sut.GetToken();
            
            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://token.endpoint/oauth2/token");
            message.Method.Should().Be(HttpMethod.Post);
            (await message.Content.ReadAsStringAsync()).Should().Be(expected);
        }
        
        [Fact]
        public async Task GetToken_CallsTokenEndpoint_WithCorrectObject_SpecifiedScope()
        {
            // Assert
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            var httpResponseMessage = httpHandler.GetResponseMessage(
                "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.SetResponse(httpResponseMessage);

            var expected =
                "grant_type=client_credentials&client_id=super-secret&client_secret=earfquake&scope=new-scope";
            
            // Act
            await sut.GetToken("new-scope");
            
            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://token.endpoint/oauth2/token");
            message.Method.Should().Be(HttpMethod.Post);
            (await message.Content.ReadAsStringAsync()).Should().Be(expected);
        }
        
        [Fact]
        public async Task GetToken_ReturnsExpectedToken()
        {
            // Assert
            var httpResponseMessage = httpHandler.GetResponseMessage(
                "{\"access_token\": \"abc123\", \"token_type\": \"jwt\", \"expires_in\": 1}", HttpStatusCode.OK);
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.SetResponse(httpResponseMessage);

            var expected = new WellcomeApiToken
            {
                TokenType = "jwt", ExpiresIn = 1, AccessToken = "abc123"
            };

            // Act
            var token = await sut.GetToken();
            
            // Assert
            token.Should().BeEquivalentTo(expected, opts => opts.Excluding(t => t.Acquired));
        }
    }
}