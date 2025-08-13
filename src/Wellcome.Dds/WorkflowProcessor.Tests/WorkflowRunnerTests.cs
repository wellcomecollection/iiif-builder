using System;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Xunit;

namespace WorkflowProcessor.Tests
{
    public class WorkflowRunnerTests
    {
        private readonly IIngestJobRegistry ingestJobRegistry;
        private readonly WorkflowRunner sut;

        public WorkflowRunnerTests()
        {
            var runnerOptions = new RunnerOptions
            {
                RegisterImages = true
            };
            var ddsOptions = new DdsOptions
            {
            };
            var runnerOptionsInst = Options.Create(runnerOptions);
            var ddsOptionsInst = Options.Create(ddsOptions);
            
            ingestJobRegistry = A.Fake<IIngestJobRegistry>();
            var identityService = new ParsingIdentityService(new NullLogger<ParsingIdentityService>(), new MemoryCache(new MemoryCacheOptions()));
            sut = new WorkflowRunner(
                ingestJobRegistry, 
                runnerOptionsInst, 
                new NullLogger<WorkflowRunner>(),
                null, 
                null, 
                null, 
                ddsOptionsInst, 
                null, 
                null, 
                null, 
                A.Fake<ICacheInvalidationPathPublisher>(), 
                null,
                identityService);
        }
        
        [Fact]
        public async Task ProcessJob_CallsRegisterImage_IfRegisterImagesTrue()
        {
            // Arrange
            var job = new WorkflowJob{ Identifier = "b99988877"};
            var identity = IdentityHelper.GetSimpleTestBNumber("b99988877");
            
            // Act
            await sut.ProcessJob(job);
            
            // Assert
            A.CallTo(() => ingestJobRegistry.RegisterImages(identity, false)).MustHaveHappened();
        }
        
        [Fact]
        public async Task ProcessJob_SetsDlcsProperties_FromReturnedBatch()
        {
            // Arrange
            var job = new WorkflowJob{ Identifier = "b9998887"};
            var identity = IdentityHelper.GetSimpleTestBNumber("b99988877");
            var jobs = new []
            {
                new DlcsIngestJob("4") {Id = 4}, new DlcsIngestJob("1") {Id = 1},
            };
            A.CallTo(() => ingestJobRegistry.RegisterImages(identity, false)).Returns(jobs);
            
            // Act
            await sut.ProcessJob(job);
            
            // Assert
            job.FirstDlcsJobId.Should().Be(4);
            job.DlcsJobCount.Should().Be(2);
        }
        
        [Fact]
        public async Task ProcessJob_SetsTimingProperties()
        {
            // Arrange
            var job = new WorkflowJob{ Identifier = "b99988877"};
            
            // Act
            await sut.ProcessJob(job);
            
            // Assert
            job.Taken.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            job.TotalTime.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public async Task ProcessJob_HandlesNullBatch()
        {
            // Arrange
            var job = new WorkflowJob{ Identifier = "b99988877"};
            var identity = IdentityHelper.GetSimpleTestBNumber("b99988877");
            A.CallTo(() => ingestJobRegistry.RegisterImages(identity, false)).Returns((DlcsIngestJob[])null);
            
            // Act
            await sut.ProcessJob(job);
            
            // Assert
            job.FirstDlcsJobId.Should().Be(0);
            job.DlcsJobCount.Should().Be(0);
        }
        
        [Fact]
        public async Task ProcessJob_HandlesEmptyBatch()
        {
            // Arrange
            var job = new WorkflowJob{ Identifier = "b99988877"};
            var identity = IdentityHelper.GetSimpleTestBNumber("b99988877");
            var jobs = new DlcsIngestJob [0];
                
            A.CallTo(() => ingestJobRegistry.RegisterImages(identity, false)).Returns(jobs);
            
            // Act
            await sut.ProcessJob(job);
            
            // Assert
            job.FirstDlcsJobId.Should().Be(0);
            job.DlcsJobCount.Should().Be(0);
        }
    }
}