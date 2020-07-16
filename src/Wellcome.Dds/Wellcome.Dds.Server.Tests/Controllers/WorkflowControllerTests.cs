﻿using System;
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
        public async Task GetCreate_NewBNumber_Returns202_AndCreatesJobInDatabase()
        {
            // Arrange
            dbFixture.CleanUp();
            const string bnumber = "b1231231";
            var requestUri = $"/workflow/create/{bnumber}";

            var expected = new WorkflowJob
            {
                Identifier = bnumber,
                Waiting = true,
                Finished = false,
                Error = null,
                ForceTextRebuild = true
            };
            
            // Act
            var response = await client.GetAsync(requestUri);
            
            // Assert
            response.StatusCode.Should().Be(202);

            var actual = await response.Content.ReadAsAsync<WorkflowJob>();
            actual.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild));

            var fromDatabase = await dbFixture.DdsInstrumentationContext.WorkflowJobs.FindAsync(bnumber);
            fromDatabase.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild));
        }
        
        [Fact]
        public async Task GetCreate_ExistingBNumber_Returns202_AndUpdatesJobInDatabase()
        {
            // Arrange
            dbFixture.CleanUp();
            const string bnumber = "b1231232";
            var requestUri = $"/workflow/create/{bnumber}";

            var expected = new WorkflowJob
            {
                Identifier = bnumber,
                Waiting = true,
                Finished = false,
                Error = null,
                ForceTextRebuild = true
            };

            await dbFixture.DdsInstrumentationContext.WorkflowJobs.AddAsync(new WorkflowJob
            {
                Identifier = bnumber,
                Created = DateTime.Today.AddYears(-1),
                Waiting = false
            });
            await dbFixture.DdsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var response = await client.GetAsync(requestUri);
            
            // Assert
            response.StatusCode.Should().Be(202);

            var actual = await response.Content.ReadAsAsync<WorkflowJob>();
            actual.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild));

            var fromDatabase =
                await dbFixture.DdsInstrumentationContext.WorkflowJobs.SingleOrDefaultAsync(
                    j => j.Identifier == bnumber);
            fromDatabase.Should().BeEquivalentTo(expected, opts => 
                opts.Including(job => job.Identifier)
                    .Including(job => job.Waiting)
                    .Including(job => job.Finished)
                    .Including(job => job.ForceTextRebuild));
            fromDatabase.Created.Should().BeCloseTo(DateTime.Now, 5000); // created reset- is that right?
        }
    }
}