using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Xunit;

namespace WorkflowProcessor.Tests
{
    public class WorkflowJobPostProcessorTests
    {
        private readonly WorkflowJobPostProcessor sut;
        private readonly IAmazonSimpleNotificationService sns;
        
        public WorkflowJobPostProcessorTests()
        {
            sns = A.Fake<IAmazonSimpleNotificationService>();
            
            var invalidationOptions = new CacheInvalidationOptions
            {
                InvalidateApiTopicArn = "api:arn:topic",
                InvalidateIIIFTopicArn = "iiif:arn:topic"
            };

            var uriPatterns = new UriPatterns(Options.Create(new DdsOptions()));
            sut = new WorkflowJobPostProcessor(sns, uriPatterns,
                Options.Create(invalidationOptions), NullLogger<WorkflowJobPostProcessor>.Instance);
        }

        [Fact]
        public async Task PostProcess_DoesNothing_IfFlushCacheFalse()
        {
            // Arrange
            var workflowJob = new WorkflowJob {FlushCache = false};
            
            // Act
            await sut.PostProcess(workflowJob);
            
            // Assert
            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(6)]
        public async Task PostProcess_FlushesIIIFCache_IfInvalidateTrue(int options)
        {
            // Arrange
            var workflowJob = new WorkflowJob
            {
                FlushCache = true, Identifier = "b1231231", WorkflowOptions = options
            };
            PublishRequest request = null;
            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
                .Invokes((PublishRequest pr, CancellationToken ct) => request = pr);

            // Act
            await sut.PostProcess(workflowJob);
            
            // Assert
            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            request.TopicArn.Should().Be("iiif:arn:topic");
            
            // don't check individual paths, UriPatterns can't be faked so just verify number
            var message = JToken.Parse(request.Message);
            message.Value<string>("reference").Should().StartWith("dds-b1231231-");
            message.Value<JArray>("paths").Should().HaveCount(11);
        }
        
        [Theory]
        [InlineData(8)]
        [InlineData(24)]
        public async Task PostProcess_FlushesIIIFCacheAndApiCache_IfInvalidateTrue_AndOptionsText(int options)
        {
            // Arrange
            var workflowJob = new WorkflowJob
            {
                FlushCache = true, Identifier = "b1231231", WorkflowOptions = options
            };
            PublishRequest requestIiif = null;
            PublishRequest requestApi = null;
            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
                .Invokes((PublishRequest pr, CancellationToken ct) =>
                {
                    if (pr.TopicArn.Contains("iiif"))
                    {
                        requestIiif = pr;
                    }
                    else
                    {
                        requestApi = pr;
                    }
                });

            // Act
            await sut.PostProcess(workflowJob);
            
            // Assert
            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
            
            requestIiif.TopicArn.Should().Be("iiif:arn:topic");
            var messageIiif = JToken.Parse(requestIiif.Message);
            messageIiif.Value<string>("reference").Should().StartWith("dds-b1231231-");
            messageIiif.Value<JArray>("paths").Should().HaveCount(11);
            
            requestApi.TopicArn.Should().Be("api:arn:topic");
            var messageApi = JToken.Parse(requestApi.Message);
            messageApi.Value<string>("reference").Should().StartWith("dds-b1231231-");
            messageApi.Value<JArray>("paths").Should().HaveCount(2);
        }
    }
}