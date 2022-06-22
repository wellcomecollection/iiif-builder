using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Server.Tests.Integration;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Controllers
{
    [Trait("Category","Integration")]
    [Trait("Category","Database")]
    [Collection(nameof(DatabaseFixture))]
    public class WorkflowControllerTests : IClassFixture<DdsServerAppFactory>
    {
        private readonly DatabaseFixture dbFixture;
        private readonly HttpClient client;

        public WorkflowControllerTests(DatabaseFixture dbFixture, DdsServerAppFactory factory)
        {
            this.dbFixture = dbFixture;
            client = factory.CreateClient();
        }

        [Fact]
        public async Task GetProcess_NewBNumber_Returns202_AndCreatesJobInDatabase()
        {
            // Arrange
            dbFixture.CleanUp();
            const string bnumber = "b1231231";
            var requestUri = $"/workflow/process/{bnumber}";

            var expected = new WorkflowJob
            {
                Identifier = bnumber,
                Waiting = true,
                Finished = false,
                Error = null,
                ForceTextRebuild = true,
                WorkflowOptions = null,
                Expedite = false,
                FlushCache = false
            };
            
            // Act
            var response = await client.GetAsync(requestUri);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var actual = await response.Content.ReadAsAsync<WorkflowJob>();
            actual.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild)
                    .Including(job => job.WorkflowOptions)
                    .Including(job => job.Expedite)
                    .Including(job => job.FlushCache));

            var fromDatabase = await dbFixture.DdsInstrumentationContext.WorkflowJobs.FindAsync(bnumber);
            fromDatabase.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild)
                    .Including(job => job.WorkflowOptions)
                    .Including(job => job.Expedite)
                    .Including(job => job.FlushCache));
        }
        
        [Fact]
        public async Task GetProcess_ExistingBNumber_Returns202_AndUpdatesJobInDatabase()
        {
            // Arrange
            dbFixture.CleanUp();
            const string bnumber = "b1231232";
            var requestUri = $"/workflow/process/{bnumber}";

            var expected = new WorkflowJob
            {
                Identifier = bnumber,
                Waiting = true,
                Finished = false,
                Error = null,
                ForceTextRebuild = true,
                WorkflowOptions = null,
                Expedite = false,
                FlushCache = false
            };

            await dbFixture.DdsInstrumentationContext.WorkflowJobs.AddAsync(new WorkflowJob
            {
                Identifier = bnumber,
                Created = DateTime.UtcNow.AddYears(-1),
                Waiting = false
            });
            await dbFixture.DdsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var response = await client.GetAsync(requestUri);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var actual = await response.Content.ReadAsAsync<WorkflowJob>();
            actual.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild)
                    .Including(job => job.WorkflowOptions)
                    .Including(job => job.Expedite)
                    .Including(job => job.FlushCache));

            var fromDatabase =
                await dbFixture.DdsInstrumentationContext.WorkflowJobs.SingleOrDefaultAsync(
                    j => j.Identifier == bnumber);
            fromDatabase.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild)
                    .Including(job => job.WorkflowOptions)
                    .Including(job => job.Expedite)
                    .Including(job => job.FlushCache));
            fromDatabase.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}