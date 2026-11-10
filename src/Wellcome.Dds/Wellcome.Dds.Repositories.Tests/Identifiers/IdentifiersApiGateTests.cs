using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Identifiers;
using Wellcome.Dds.Repositories.Identifiers;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Identifiers;

/// <summary>
/// The Identifiers API is only used where it is configured; everywhere else the DDS behaves as it did
/// before the API existed.
/// </summary>
public class IdentifiersApiGateTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("key", null, true)]
    [InlineData(null, true, true)]
    [InlineData("key", false, false)]
    public void Enabled_Means_A_Key_Unless_Set_Explicitly(string? apiKey, bool? enabled, bool expected)
    {
        new IdentifiersApiOptions { ApiKey = apiKey, Enabled = enabled }.IsEnabled.Should().Be(expected);
    }

    [Fact]
    public async Task The_Disabled_Client_Answers_No_Mapping_For_Everything_And_Says_It_Was_Skipped()
    {
        var api = new DisabledIdentifiersApi(Options.Create(new IdentifiersApiOptions()));

        api.IsEnabled.Should().BeFalse();
        (await api.GetByCanonicalId("vsyg427x")).Should().BeNull();
        (await api.GetBySourceIdentifier(SourceSystems.Mets, "b14658197")).Should().BeNull();
        (await api.GetSiblingsBySourceIdentifier(SourceSystems.Mets, "b14658197")).Should().BeNull();
        var resolution = await api.Resolve("b14658197");
        resolution.Disabled.Should().BeTrue();
        resolution.Lookups.Should().BeEmpty();
        resolution.AnyFound.Should().BeFalse();
        // the URLs are still right, so the dashboard can show what would be asked
        api.GetApiUrl(SourceSystems.Mets, "ARTCOO/B/15", OntologyType.Work, true)
            .Should().Be("https://api.wellcomecollection.org/identifiers/v1/by-source/mets/ARTCOO%2FB%2F15?type=Work&include=siblings");
    }

    [Theory]
    [InlineData(null, typeof(DisabledIdentifiersApi))]
    [InlineData("a-key", typeof(WellcomeCollectionIdentifiersApi))]
    public void Registration_Picks_The_Client_From_Configuration(string? apiKey, System.Type expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["IdentifiersApi:ApiKey"] = apiKey })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentifiersApi(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IIdentifiersApi>().Should().BeOfType(expected);
    }
}
