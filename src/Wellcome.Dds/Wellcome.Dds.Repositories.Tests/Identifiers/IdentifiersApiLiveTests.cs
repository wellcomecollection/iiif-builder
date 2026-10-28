using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Identifiers;
using Wellcome.Dds.Repositories.Identifiers;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Identifiers;

/// <summary>
/// Calls the real Identifiers API. Excluded from CI by the Category filter; set the
/// IDENTIFIERS_API_KEY environment variable to run locally.
/// </summary>
[Trait("Category", "Manual")]
public class IdentifiersApiLiveTests
{
    private readonly WellcomeCollectionIdentifiersApi sut;
    private readonly bool hasKey;

    public IdentifiersApiLiveTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("IDENTIFIERS_API_KEY");
        hasKey = !string.IsNullOrWhiteSpace(apiKey);
        var options = Options.Create(new IdentifiersApiOptions { ApiKey = apiKey });
        sut = new WellcomeCollectionIdentifiersApi(
            options, new HttpClient(), NullLogger<WellcomeCollectionIdentifiersApi>.Instance);
    }

    [Fact]
    public async Task Forward_And_Reverse_Agree_For_A_Digitised_B_Number()
    {
        if (!hasKey) return;

        var canonicalIdRef = await sut.GetBySourceIdentifier(SourceSystems.SierraSystemNumber, "b14658197");
        canonicalIdRef.Should().NotBeNull();
        canonicalIdRef.CanonicalId.Should().Be("nydjbrr7");

        var set = await sut.GetByCanonicalId(canonicalIdRef.CanonicalId);
        set.Should().NotBeNull();
        set.Original.Should().NotBeNull();
        set.Original.SourceSystem.Should().Be(SourceSystems.SierraSystemNumber);
        set.Original.Value.Should().Be("b14658197");
        set.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Resolve_B_Number_Finds_Sierra_And_Mets_Mappings()
    {
        if (!hasKey) return;

        var resolution = await sut.Resolve("b14658197");

        resolution.AnyFound.Should().BeTrue();
        resolution.Lookups.Should().Contain(l => l.Candidate.SourceSystem == SourceSystems.SierraSystemNumber && l.Found);
        resolution.Lookups.Should().Contain(l => l.Candidate.SourceSystem == SourceSystems.Mets && l.Found);
    }

    [Fact]
    public async Task Resolve_Born_Digital_Calm_Ref_Finds_Mets_And_Calm_Mappings()
    {
        if (!hasKey) return;

        var resolution = await sut.Resolve("ARTCOO/B/15");

        var mets = resolution.Lookups.Single(l => l.Candidate.SourceSystem == SourceSystems.Mets);
        mets.Found.Should().BeTrue();
        mets.IdentifierSet.Original.Value.Should().Be("artcoo/b/15");
        var calm = resolution.Lookups.Single(l => l.Candidate.SourceSystem == SourceSystems.CalmRefNo);
        calm.Found.Should().BeTrue();
        calm.IdentifierSet.Original.Value.Should().Be("ARTCOO/B/15");
        mets.IdentifierSet.CanonicalId.Should().NotBe(calm.IdentifierSet.CanonicalId);

        var preservation = resolution.PreservationIdentifierLookups.Should().ContainSingle().Which;
        preservation.IdentifierSet.PreservationIdentifier.Value.Should().Be("artcoo/b/15");
    }

    [Fact]
    public async Task Unknown_Canonical_Id_Is_Null_And_Malformed_Is_Exception()
    {
        if (!hasKey) return;

        (await sut.GetByCanonicalId("zzzzzzzz")).Should().BeNull();

        var act = () => sut.GetByCanonicalId("b14658197");
        var ex = (await act.Should().ThrowAsync<IdentifiersApiException>()).Which;
        ex.Error.Error.Should().Be("badRequest");
    }
}
