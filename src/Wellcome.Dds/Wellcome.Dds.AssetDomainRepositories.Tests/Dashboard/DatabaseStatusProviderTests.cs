using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomainRepositories.Control;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.Common;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Dashboard
{
    public class DatabaseStatusProviderTests
    {
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        
        public DatabaseStatusProviderTests()
        {
            // WARNING - using in-memory database. This acts differently from Postgres.
            // using here as the queries in sut are very simple so behaviour won't differ across db providers
            ddsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseInMemoryDatabase(nameof(DashboardRepositoryTests)).Options);
        }
        
        public DatabaseStatusProvider GetSut(DateTime? earliestJob = null, int? minimumJobAge = null)
        {
            var ddsOptions = new DdsOptions
            {
                EarliestJobDateTime = earliestJob?.ToString(),
                MinimumJobAgeMinutes = minimumJobAge ?? 0
            };

            return new DatabaseStatusProvider(
                Options.Create(ddsOptions),
                ddsInstrumentationContext,
                new NullLogger<DatabaseStatusProvider>());

        }

        [Fact]
        public void EarliestJobToTake_ReturnsCorrectValue()
        {
            // Arrange
            var date = DateTime.Today.AddDays(-4);
            
            // Act
            var sut = GetSut(date);
            
            // Assert
            sut.EarliestJobToTake.Should().Be(date);
        }
        
        [Fact]
        public void EarliestJobToTake_Null_IfNoValue()
        {
            // Act
            var sut = GetSut();
            
            // Assert
            sut.EarliestJobToTake.Should().BeNull();
        }
        
        [Fact]
        public void LatestJobToTake_Null_IfNoMinJobAge()
        {
            // Act
            var sut = GetSut();
            
            // Assert
            sut.LatestJobToTake.Should().BeNull();
        }
        
        [Fact]
        public void LatestJobToTake_ReturnsNowMinusMins_IfNotNull()
        {
            // Arrange
            const int minAge = 34;
            var expected = DateTime.UtcNow.AddMinutes(-minAge); 
            
            // Act
            var sut = GetSut(minimumJobAge: minAge);
            
            // Assert
            sut.LatestJobToTake.Should().BeCloseTo(expected, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task Start_CreatesNewControlFlow()
        {
            // Arrange
            var sut = GetSut();
            var beforeCount = await ddsInstrumentationContext.ControlFlows.CountAsync();
            
            // Act
            var result = await sut.Start();
            
            // Assert
            result.Should().BeTrue();
            var controlFlow = await ddsInstrumentationContext.ControlFlows.OrderByDescending(cf => cf.Id).FirstAsync();
            controlFlow.CreatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            
            var afterCount = await ddsInstrumentationContext.ControlFlows.CountAsync();
            afterCount.Should().Be(beforeCount + 1);
        }
        
        [Fact]
        public async Task Stop_MarksLatestControlFlow_AsStopped()
        {
            // Arrange
            var sut = GetSut();
            var controlFlow = new ControlFlow
            {
                CreatedOn = DateTime.UtcNow,
                StoppedOn = null
            };
            await ddsInstrumentationContext.ControlFlows.AddAsync(new ControlFlow
                {CreatedOn = DateTime.UtcNow.AddDays(1)});
            await ddsInstrumentationContext.ControlFlows.AddAsync(controlFlow);
            await ddsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var result = await sut.Stop();
            
            // Assert
            result.Should().BeTrue();
            var updated = await ddsInstrumentationContext.ControlFlows.FindAsync(controlFlow.Id);
            updated.StoppedOn.Should().NotBeNull();
        }
        
        [Fact]
        public async Task GetHeartbeat_Null_IfLatestHasNoHeartbeat()
        {
            // Arrange
            var sut = GetSut();
            var controlFlows = new List<ControlFlow>
            {
                new()
                {
                    CreatedOn = DateTime.UtcNow,
                    Heartbeat = DateTime.UtcNow
                },
                new()
                {
                    CreatedOn = DateTime.UtcNow,
                    Heartbeat = null
                }
            };
            await ddsInstrumentationContext.ControlFlows.AddRangeAsync(controlFlows);
            await ddsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var heartbeat = await sut.GetHeartbeat();
            
            // Assert
            heartbeat.Should().BeNull();
        }
        
        [Fact]
        public async Task GetHeartbeat_ReturnsLatestRecordHeartbeat_IfPresent()
        {
            // Arrange
            var sut = GetSut();
            var controlFlows = new List<ControlFlow>
            {
                new()
                {
                    CreatedOn = DateTime.UtcNow,
                    Heartbeat = null
                },
                new()
                {
                    CreatedOn = DateTime.UtcNow,
                    Heartbeat = DateTime.UtcNow
                }
            };
            await ddsInstrumentationContext.ControlFlows.AddRangeAsync(controlFlows);
            await ddsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var heartbeat = await sut.GetHeartbeat();
            
            // Assert
            heartbeat.Should().NotBeNull();
        }
        
        [Fact]
        public async Task WriteHeartbeat_Updates_LatestRecord()
        {
            // Arrange
            var sut = GetSut();
            var controlFlow = new ControlFlow
            {
                CreatedOn = DateTime.UtcNow,
                Heartbeat = DateTime.UtcNow.AddDays(-16)
            };
            await ddsInstrumentationContext.ControlFlows.AddAsync(new ControlFlow
                {CreatedOn = DateTime.UtcNow.AddDays(1)});
            await ddsInstrumentationContext.ControlFlows.AddAsync(controlFlow);
            await ddsInstrumentationContext.SaveChangesAsync();
            
            // Act
            var result = await sut.WriteHeartbeat();
            
            // Assert
            var updated = await ddsInstrumentationContext.ControlFlows.FindAsync(controlFlow.Id);
            updated.Heartbeat.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            updated.Heartbeat.Should().Be(result);
        }
    }
}