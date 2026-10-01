using System;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests;

public class PersistedIdentityServiceTests
{
    private static PersistedIdentityService GetSut(Action<DbContextOptionsBuilder> configureDb)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DdsContext>(configureDb);
        var provider = services.BuildServiceProvider();
        return new PersistedIdentityService(
            NullLogger<PersistedIdentityService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            provider.GetRequiredService<IServiceScopeFactory>(),
            null, // storage service is only used on the write path
            A.Fake<ICatalogue>(),
            Options.Create(new DdsOptions()));
    }

    private static PersistedIdentityService GetSutWithInMemoryDb(Action<DdsContext> seed = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var sut = GetSut(o => o.UseInMemoryDatabase(dbName));
        if (seed != null)
        {
            var options = new DbContextOptionsBuilder<DdsContext>().UseInMemoryDatabase(dbName).Options;
            using var ctx = new DdsContext(options);
            seed(ctx);
            ctx.SaveChanges();
        }
        return sut;
    }

    [Fact]
    public void Parsed_Calm_Identity_Does_Not_Leak_Its_Casing_To_Other_Requests()
    {
        // Arrange - no stored record, so the identity is parsed from the request
        var sut = GetSutWithInMemoryDb();

        // Act
        var wrongCase = sut.GetIdentity("ppcri/a/1");
        var rightCase = sut.GetIdentity("PPCRI/A/1");

        // Assert
        wrongCase.Value.Should().Be("ppcri/a/1");
        rightCase.Value.Should().Be("PPCRI/A/1");
    }

    [Fact]
    public void Stored_Calm_Identity_Supplies_Canonical_Casing()
    {
        // Arrange
        var sut = GetSutWithInMemoryDb(ctx =>
        {
            var stored = ParsingIdentityService.Parse("PPCRI/A/1");
            stored.FromGenerator = true;
            ctx.Identities.Add(stored);
        });

        // Act
        var identity = sut.GetIdentity("ppcri_a_1");

        // Assert
        identity.Value.Should().Be("PPCRI/A/1");
        identity.FromGenerator.Should().BeTrue();
    }

    [Theory]
    [InlineData("b13248169", "b13248169")]
    [InlineData("PPCRI/A/1", "PPCRI/A/1")]
    public void Read_Falls_Back_To_Parsed_Identity_When_Database_Unavailable(string requested, string expectedValue)
    {
        // Arrange - nothing listens on port 1, so the connection is refused immediately
        var sut = GetSut(o => o.UseNpgsql("Host=127.0.0.1;Port=1;Timeout=1"));

        // Act
        var identity = sut.GetIdentity(requested);

        // Assert
        identity.Value.Should().Be(expectedValue);
        identity.FromGenerator.Should().BeFalse();
    }
}
