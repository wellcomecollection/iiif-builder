using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Test.Helpers;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.Common;
using Xunit;

namespace DlcsWebClient.Tests.Dlcs
{
    public class DlcsTests
    {
        private readonly ControllableHttpMessageHandler httpHandler;
        private readonly DlcsWebClient.Dlcs.Dlcs sut;
        private readonly JsonSerializerSettings serializer;

        public DlcsTests()
        {
            var dlcsOptions = new DlcsOptions
            {
                CustomerId = 99,
                CustomerName = "tester",
                CustomerDefaultSpace = 15,
                BatchSize = 5,
                ApiEntryPoint = "https://api.dlcs.test/",
                ResourceEntryPoint = "https://dlcs.test/",
                PdfQueryName = "pdf-q"
            };
            httpHandler = new ControllableHttpMessageHandler();
            var httpClient = new HttpClient(httpHandler);
            var options = Options.Create(dlcsOptions);
            
            var identityService = new ParsingIdentityService(new MemoryCache(new MemoryCacheOptions()));
            sut = new DlcsWebClient.Dlcs.Dlcs(new NullLogger<DlcsWebClient.Dlcs.Dlcs>(), options, httpClient, identityService);
            
            // NOTE - not verifying what is sent - just ensuring serialization is correct
            serializer = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }
        
        [Fact]
        public async Task RegisterImages_PostsToCorrectUri_NotPriority()
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var request = new HydraImageCollection {Members = images};

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.RegisterImages(request, new DlcsCallContext("[test]", "[id]"), false);

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://api.dlcs.test/customers/99/queue");
            message.Method.Should().Be(HttpMethod.Post);
        }
        
        [Fact]
        public async Task RegisterImages_PostsToCorrectUri_Priority()
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var request = new HydraImageCollection {Members = images};
            
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.RegisterImages(request, new DlcsCallContext("[test]", "[id]"), true);

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://api.dlcs.test/customers/99/queue/priority");
            message.Method.Should().Be(HttpMethod.Post);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RegisterImages_PostsSerializedHydraBody(bool priority)
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var request = new HydraImageCollection {Members = images};

            var expected = JsonConvert.SerializeObject(request, Formatting.Indented, serializer);

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.RegisterImages(request, new DlcsCallContext("[test]", "[id]"), priority);

            // Assert
            (await message.Content.ReadAsStringAsync()).Should().Be(expected);
        }

        [Fact]
        public async Task PatchImages_PatchesToCorrectUri()
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var request = new HydraImageCollection {Members = images};

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.PatchImages(request, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://api.dlcs.test/customers/99/spaces/15/images");
            message.Method.Should().Be(HttpMethod.Patch);
        }
        
        [Fact]
        public async Task PatchImages_PatchesSerializedHydraBody()
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(1)
                .All()
                .With(m => m.ModelId, "space/img")
                .Build()
                .ToArray();
            
            var request = new HydraImageCollection {Members = images};
            
            var expected = JsonConvert.SerializeObject(request, Formatting.Indented, serializer);

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.PatchImages(request, new DlcsCallContext("[test]", "[id]"));

            // Assert
            (await message.Content.ReadAsStringAsync()).Should().Be(expected);
        }
        
        [Fact]
        public async Task PatchImages_UpdatesModelId_IfIncorrectFormat()
        {
            // Arrange
            var images = Builder<Image>.CreateListOfSize(1)
                .All()
                .With(m => m.ModelId, "noslash")
                .Build()
                .ToArray();

            var expectedModelId = "99/15/noslash";
            var request = new HydraImageCollection {Members = images};
            
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.PatchImages(request, new DlcsCallContext("[test]", "[id]"));

            // Assert
            var actual = await message.Content.ReadAsAsync<HydraImageCollection>();
            actual.Members.Single().ModelId.Should().Be(expectedModelId);
        }
        
        [Fact]
        public async Task GetImages_CallsCorrectUri_UsingDefaultSpaceIfNoSpaceSpecified()
        {
            // Arrange
            var imageQuery = new ImageQuery{Number1 = 123};

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetImages(imageQuery,999, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be($"https://api.dlcs.test/customers/99/spaces/999/images?q={{{Environment.NewLine}  \"number1\": 123{Environment.NewLine}}}");
            message.Method.Should().Be(HttpMethod.Get);
        }
        
        [Fact]
        public async Task GetImages_CallsCorrectUri_UsingSpecifiedSpace()
        {
            // Arrange
            var imageQuery = new ImageQuery {Space = 7};

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetImages(imageQuery, 999, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be($"https://api.dlcs.test/customers/99/spaces/7/images?q={{{Environment.NewLine}  \"space\": 7{Environment.NewLine}}}");
            message.Method.Should().Be(HttpMethod.Get);
        }
        
        [Fact]
        public async Task GetImages_NextUri_CallsCorrectUri()
        {
            // Arrange
            const string uri = "https://api.dlcs.test/customers/99/spaces/999/images?page=hi";

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetImages(uri, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be(uri);
            message.Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public async Task GetBatch_CallsCorrectUri()
        {
            // Arrange
            const string batchId = "123123";

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetBatch(batchId, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be("https://api.dlcs.test/customers/99/queue/batches/123123");
            message.Method.Should().Be(HttpMethod.Get);
        }
        
        [Fact]
        public async Task GetBatch_UsesPassedInBatchIdAsUri_IfValidUri()
        {
            // Arrange
            const string batchId = "https://api.dlcs.test/customers/get-the-batch";

            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetBatch(batchId, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be(batchId);
            message.Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public void GetRoleUri_ReturnsClickThrough_ForRequiresRegistration()
        {
            // Act
            var roleUriRr = sut.GetRoleUri(AccessCondition.RequiresRegistration);
            var roleUriOa = sut.GetRoleUri(AccessCondition.OpenWithAdvisory);
    
            // Assert
            // NOTE THAT THIS IS NOT api.dlcs.test - roles are Global URIs
            var expected = "https://api.dlcs.io/customers/99/roles/clickthrough";
            roleUriRr.Should().Be(expected);
            roleUriOa.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(AccessCondition.Open, "https://api.dlcs.io/customers/99/roles/open")]
        [InlineData(AccessCondition.ClinicalImages, "https://api.dlcs.io/customers/99/roles/clinicalImages")]
        [InlineData(AccessCondition.RestrictedFiles, "https://api.dlcs.io/customers/99/roles/restrictedFiles")]
        [InlineData(AccessCondition.Closed, "https://api.dlcs.io/customers/99/roles/closed")]
        public void GetRoleUri_ReturnsExpected_ForNonRequiresRegistration(string accessCondition, string expected)
        {
            // Act
            var roleUri = sut.GetRoleUri(accessCondition);

            // Assert
            roleUri.Should().Be(expected);
        }

        [Fact]
        public async Task GetImagesForIssue_CallsCorrectUri()
        {
            // Arrange
            const string issueIdentifier = "issue-ident";
            
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var response = new HydraImageCollection {Members = images};
            httpHandler.SetResponse(httpHandler.GetResponseMessage(JsonConvert.SerializeObject(response), HttpStatusCode.OK));
            
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);

            // Act
            await sut.GetImagesForIssue(issueIdentifier, new DlcsCallContext("[test]", "[id]"));

            // Assert
            httpHandler.CallsMade.Should().ContainSingle()
                .Which.Should().Be($"https://api.dlcs.test/customers/99/spaces/15/images?q={{{Environment.NewLine}  \"string3\": \"issue-ident\"{Environment.NewLine}}}");
            message.Method.Should().Be(HttpMethod.Get);
        }

        [Theory]
        [InlineData("b1234567")]
        [InlineData("b1234567_001")]
        [InlineData("b1234567_001_002")]
        public async Task GetPdfDetails_CallsCorrectPath(string identifier)
        {
            // Arrange
            var expected = $"https://dlcs.io/pdf-control/tester/pdf-q/15/{identifier}";
            httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.NotFound));
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);
            
            // Act
            await sut.GetPdfDetails(identifier);

            // Assert
            httpHandler.CallsMade.Should().ContainSingle(s => s == expected);
            message.Method.Should().Be(HttpMethod.Get);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetPdfDetails_ReturnsNull_IfCallUnsuccessful(HttpStatusCode statusCode)
        {
            // Arrange
            httpHandler.SetResponse(new HttpResponseMessage(statusCode));
            
            // Act
            var result = await sut.GetPdfDetails("b1234123");
            
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public async Task GetPdfDetails_ReturnsPdf()
        {
            // Arrange
            var pdf = Builder<Pdf>.CreateNew().Build();

            var httpResponseMessage = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(pdf),
                HttpStatusCode.OK);
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.SetResponse(httpResponseMessage);
            
            // Act
            var result = await sut.GetPdfDetails("b1234123");
            
            // Assert
            result.Should().BeEquivalentTo(pdf);
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetPdfDetails_ReturnsPdf_WithUpdatedUrl_IfNoUrlReturned(string url)
        {
            // Arrange
            var pdf = Builder<Pdf>.CreateNew()
                .With(p => p.Url, url)
                .Build();
            
            const string expected = "https://dlcs.io/pdf/tester/pdf-q/15/b1234123";

            var httpResponseMessage = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(pdf),
                HttpStatusCode.OK);
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpHandler.SetResponse(httpResponseMessage);
            
            // Act
            var result = await sut.GetPdfDetails("b1234123");
            
            // Assert
            result.Should().BeEquivalentTo(pdf, opts => opts.Excluding(p => p.Url));
            result.Url.Should().Be(expected);
        }
        
        [Theory]
        [InlineData("b1234567")]
        [InlineData("b1234567_001")]
        [InlineData("b1234567_001_002")]
        public async Task DeletePdf_CallsCorrectPath(string identifier)
        {
            // Arrange
            var expected = $"https://api.dlcs.test/customers/99/resources/pdf/pdf-q?args=15/{identifier}";
            httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.NotFound));
            HttpRequestMessage message = null;
            httpHandler.RegisterCallback(r => message = r);
            
            // Act
            await sut.DeletePdf(identifier);

            // Assert
            httpHandler.CallsMade.Should().ContainSingle(s => s == expected);
            message.Method.Should().Be(HttpMethod.Delete);
        }
    }
}