using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Identifiers;
using Wellcome.Dds.Repositories.Identifiers;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Identifiers;

public class IdentifierNavigatorTests
{
    private readonly IIdentifiersApi identifiersApi = A.Fake<IIdentifiersApi>();
    private readonly ICatalogue catalogue = A.Fake<ICatalogue>();
    private readonly IIdentityService identityService = A.Fake<IIdentityService>();
    private readonly IStorageServiceProbe storageProbe = A.Fake<IStorageServiceProbe>();
    private readonly IdentifierNavigator sut;

    public IdentifierNavigatorTests()
    {
        A.CallTo(() => storageProbe.StorageApi).Returns("https://api-stage.example.org/storage/v1/bags/{0}/{1}");
        // default: every probe is a 404 unless a test says otherwise
        A.CallTo(() => storageProbe.Probe(A<string>._, A<string>._))
            .ReturnsLazily((string space, string id) => Task.FromResult(new StorageProbeResult(space, id) { StatusCode = 404 }));
        // default: parse-only identities
        A.CallTo(() => identityService.GetIdentity(A<string>._))
            .ReturnsLazily((string s) => ParsingIdentityService.Parse(s));
        sut = new IdentifierNavigator(identifiersApi, catalogue, identityService, storageProbe,
            NullLogger<IdentifierNavigator>.Instance);
    }

    private static SourceIdentifier Row(string sourceSystem, string value, bool isAlias = false) => new SourceIdentifier
    {
        Type = OntologyType.Work, SourceSystem = sourceSystem, Value = value, IsAlias = isAlias,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static IdentifierSet Set(string canonicalId, params SourceIdentifier[] rows) => new IdentifierSet
    {
        CanonicalId = canonicalId, Type = OntologyType.Work, SourceIdentifiers = rows
    };

    private static CandidateLookup Hit(string sourceSystem, string value, IdentifierSet set) =>
        new CandidateLookup(new IdentifierCandidate(value, sourceSystem, OntologyType.Work, "test"), "url")
        {
            IdentifierSet = set
        };

    private static Work WorkWithLocation(string id, string iiifUrl, params (string type, string value)[] identifiers) => new Work
    {
        Id = id,
        Title = "Title of " + id,
        Items = new[]
        {
            new Item
            {
                Locations = new[]
                {
                    new Location { LocationType = new LabelledEntity { Id = "iiif-presentation" }, Url = iiifUrl }
                }
            }
        },
        Identifiers = identifiers.Select(i => new Identifier
        {
            IdentifierType = new LabelledEntity { Id = i.type }, Value = i.value
        }).ToArray()
    };

    private static HttpRequestException NotFound() => new HttpRequestException("404", null, HttpStatusCode.NotFound);

    [Fact]
    public async Task Born_Digital_Calm_Ref_Navigates_Every_Route()
    {
        // Registry: mets row (lowercased) and a calm-ref-no row that is not a work
        var metsSet = Set("q8fhz52m", Row(SourceSystems.Mets, "artcoo/b/15"));
        var calmSet = Set("m2fe2wrv", Row(SourceSystems.CalmRefNo, "ARTCOO/B/15"));
        var resolution = new IdentifierResolution("ARTCOO/B/15");
        resolution.Lookups.Add(Hit(SourceSystems.Mets, "ARTCOO/B/15", metsSet));
        resolution.Lookups.Add(Hit(SourceSystems.CalmRefNo, "ARTCOO/B/15", calmSet));
        A.CallTo(() => identifiersApi.Resolve("ARTCOO/B/15")).Returns(resolution);

        // DDS has an authoritative record with the generator's casing
        var stored = ParsingIdentityService.Parse("ARTCOO/B/15");
        stored.FromGenerator = true;
        stored.CatalogueId = "vaqg68j6";
        A.CallTo(() => identityService.GetIdentity(A<string>.That.Matches(s => s.Equals("ARTCOO/B/15", StringComparison.OrdinalIgnoreCase))))
            .Returns(stored);

        // Catalogue: the mets canonical redirects to the public work; the calm-ref-no canonical is a 404
        var publicWork = WorkWithLocation("vaqg68j6", "https://iiif.wellcomecollection.org/presentation/ARTCOO/B/15",
            (SourceSystems.CalmRefNo, "ARTCOO/B/15"), (SourceSystems.Mets, "artcoo/b/15"));
        A.CallTo(() => catalogue.GetWorkByWorkId("q8fhz52m")).Returns(publicWork);
        A.CallTo(() => catalogue.GetWorkByWorkId("m2fe2wrv")).Throws(NotFound());
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("ARTCOO/B/15")).Returns(publicWork);

        // Storage: born-digital hit
        A.CallTo(() => storageProbe.Probe(StorageSpace.BornDigital, "ARTCOO/B/15"))
            .Returns(new StorageProbeResult(StorageSpace.BornDigital, "ARTCOO/B/15")
            {
                Found = true, Version = "v3", Bucket = "bucket", Path = "born-digital/ARTCOO/B/15"
            });

        var nav = await sut.Navigate("ARTCOO/B/15");

        nav.DdsIdentity.Should().BeSameAs(stored);
        var preservation = nav.PreservationIdentifiers.Should().ContainSingle().Which;
        preservation.Value.Should().Be("artcoo/b/15");
        preservation.CasedValue.Should().Be("ARTCOO/B/15");
        preservation.CasingSource.Should().Be("DDS identity record");
        preservation.MetsCanonicalId.Should().Be("q8fhz52m");

        nav.PublicWorks.Should().HaveCount(2);
        var viaMets = nav.PublicWorks.Single(p => p.RegistryCanonicalId == "q8fhz52m");
        viaMets.Found.Should().BeTrue();
        viaMets.Redirected.Should().BeTrue();
        viaMets.Work.Id.Should().Be("vaqg68j6");
        var viaCalm = nav.PublicWorks.Single(p => p.RegistryCanonicalId == "m2fe2wrv");
        viaCalm.Found.Should().BeFalse();
        viaCalm.StatusCode.Should().Be(404);

        nav.WorkByIdentifiersQuery.Id.Should().Be("vaqg68j6");
        nav.RoutesAgree.Should().BeTrue();
        nav.PreservationIdentifiersFromWork.Should().Equal("ARTCOO/B/15");

        // Only the correctly cased value is ever sent to storage, and probing stops at the hit
        nav.StorageProbes.Select(p => (p.StorageSpace, p.Identifier, p.Found)).Should().Equal(
            (StorageSpace.Digitised, "ARTCOO/B/15", false),
            (StorageSpace.BornDigital, "ARTCOO/B/15", true));
        nav.PreservedFilesFound.Should().ContainSingle().Which.Version.Should().Be("v3");
        nav.StorageApi.Should().Contain("api-stage");
    }

    [Fact]
    public async Task Casing_Falls_Back_To_Catalogue_Calm_Ref_No_When_Dds_Has_No_Record()
    {
        // Start from the METS work's canonical id: the registry only gives the lowercased mets value
        var metsSet = Set("q8fhz52m", Row(SourceSystems.Mets, "artcoo/b/15"));
        var resolution = new IdentifierResolution("q8fhz52m");
        resolution.Lookups.Add(new CandidateLookup(new IdentifierCandidate("q8fhz52m", null, OntologyType.Work, "test"), "url")
        {
            IdentifierSet = metsSet
        });
        A.CallTo(() => identifiersApi.Resolve("q8fhz52m")).Returns(resolution);

        var publicWork = WorkWithLocation("vaqg68j6", "https://iiif.wellcomecollection.org/presentation/ARTCOO/B/15",
            (SourceSystems.CalmRefNo, "ARTCOO/B/15"));
        A.CallTo(() => catalogue.GetWorkByWorkId("q8fhz52m")).Returns(publicWork);
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("q8fhz52m")).Returns((Work) null);

        var nav = await sut.Navigate("q8fhz52m");

        var preservation = nav.PreservationIdentifiers.Should().ContainSingle().Which;
        preservation.CasedValue.Should().Be("ARTCOO/B/15");
        preservation.CasingSource.Should().Be("catalogue calm-ref-no");
        nav.WorkByIdentifiersQuery.Should().BeNull();
        nav.RoutesAgree.Should().BeNull("only one route found a work");
        // The canonical id itself is never a storage key; only the recovered preservation identifier is probed
        nav.StorageProbes.Select(p => p.Identifier).Distinct().Should().Equal("ARTCOO/B/15");
    }

    [Fact]
    public async Task Casing_Is_Unknown_When_Nothing_Can_Supply_It()
    {
        var metsSet = Set("q8fhz52m", Row(SourceSystems.Mets, "artcoo/b/15"));
        var resolution = new IdentifierResolution("q8fhz52m");
        resolution.Lookups.Add(new CandidateLookup(new IdentifierCandidate("q8fhz52m", null, OntologyType.Work, "test"), "url")
        {
            IdentifierSet = metsSet
        });
        A.CallTo(() => identifiersApi.Resolve("q8fhz52m")).Returns(resolution);
        A.CallTo(() => catalogue.GetWorkByWorkId("q8fhz52m")).Throws(NotFound());
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("q8fhz52m")).Returns((Work) null);

        var nav = await sut.Navigate("q8fhz52m");

        var preservation = nav.PreservationIdentifiers.Should().ContainSingle().Which;
        preservation.CasedValue.Should().BeNull();
        preservation.CasingSource.Should().BeNull();
        preservation.ProbeValue.Should().Be("artcoo/b/15", "the lowercased value is all we have, and the probe will show it failing");
    }

    [Fact]
    public async Task B_Number_Is_Lowercase_By_Nature_And_Probing_Stops_At_First_Hit()
    {
        var metsSet = Set("f22jwsw5", Row(SourceSystems.Mets, "b14658197"));
        var sierraSet = Set("nydjbrr7", Row(SourceSystems.SierraSystemNumber, "b14658197"));
        var resolution = new IdentifierResolution("b14658197");
        resolution.Lookups.Add(Hit(SourceSystems.Mets, "b14658197", metsSet));
        resolution.Lookups.Add(Hit(SourceSystems.SierraSystemNumber, "b14658197", sierraSet));
        A.CallTo(() => identifiersApi.Resolve("b14658197")).Returns(resolution);

        var publicWork = WorkWithLocation("nydjbrr7", "https://iiif.wellcomecollection.org/presentation/v2/b14658197",
            (SourceSystems.SierraSystemNumber, "b14658197"));
        A.CallTo(() => catalogue.GetWorkByWorkId(A<string>._)).Returns(publicWork);
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("b14658197")).Returns(publicWork);
        A.CallTo(() => storageProbe.Probe(StorageSpace.Digitised, "b14658197"))
            .Returns(new StorageProbeResult(StorageSpace.Digitised, "b14658197") { Found = true, Version = "v1" });

        var nav = await sut.Navigate("b14658197");

        nav.PreservationIdentifiers.Should().ContainSingle().Which.CasingSource.Should().Be("lowercase by nature");
        nav.PublicWorks.Should().OnlyContain(p => p.Found && p.Work.Id == "nydjbrr7");
        nav.PublicWorks.Single(p => p.RegistryCanonicalId == "f22jwsw5").Redirected.Should().BeTrue();
        nav.PublicWorks.Single(p => p.RegistryCanonicalId == "nydjbrr7").Redirected.Should().BeFalse();
        nav.RoutesAgree.Should().BeTrue();
        // the v2 path segment is skipped
        nav.PreservationIdentifiersFromWork.Should().ContainSingle().Which.Should().Be("b14658197");
        nav.StorageProbes.Should().ContainSingle().Which.Found.Should().BeTrue();
        A.CallTo(() => storageProbe.Probe(StorageSpace.BornDigital, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Routes_Disagree_When_Registry_And_Query_Reach_Different_Works()
    {
        var sierraSet = Set("er85sbxs", Row(SourceSystems.SierraSystemNumber, "b18304175"));
        var resolution = new IdentifierResolution("b18304175");
        resolution.Lookups.Add(Hit(SourceSystems.SierraSystemNumber, "b18304175", sierraSet));
        A.CallTo(() => identifiersApi.Resolve("b18304175")).Returns(resolution);
        A.CallTo(() => catalogue.GetWorkByWorkId("er85sbxs")).Returns(new Work { Id = "a9jwm2jc" });
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("b18304175")).Returns(new Work { Id = "zzzzzzzz" });

        var nav = await sut.Navigate("b18304175");

        nav.RoutesAgree.Should().BeFalse();
        nav.PreservationIdentifiers.Should().BeEmpty("no mets row was found");
        // The supplied b number is still probed, because its shape says it is a preservation identifier
        nav.StorageProbes.Select(p => p.Identifier).Should().OnlyContain(id => id == "b18304175");
    }

    [Fact]
    public async Task Alt_Ref_No_Reaches_The_Work_Only_Through_The_Identifiers_Query()
    {
        // The registry never mints calm-altref-no, so every candidate misses
        var resolution = new IdentifierResolution("SA/MAC/J.3/32");
        resolution.Lookups.Add(new CandidateLookup(new IdentifierCandidate("SA/MAC/J.3/32", SourceSystems.Mets, OntologyType.Work, "test"), "url"));
        resolution.Lookups.Add(new CandidateLookup(new IdentifierCandidate("SA/MAC/J.3/32", SourceSystems.CalmRefNo, OntologyType.Work, "test"), "url"));
        A.CallTo(() => identifiersApi.Resolve("SA/MAC/J.3/32")).Returns(resolution);
        var publicWork = WorkWithLocation("a9jwm2jc", "https://iiif.wellcomecollection.org/presentation/v2/b18304175",
            (SourceSystems.CalmAltRefNo, "SA/MAC/J.3/32"), (SourceSystems.Mets, "b18304175"));
        A.CallTo(() => catalogue.GetWorkByOtherIdentifier("SA/MAC/J.3/32")).Returns(publicWork);
        A.CallTo(() => storageProbe.Probe(StorageSpace.Digitised, "b18304175"))
            .Returns(new StorageProbeResult(StorageSpace.Digitised, "b18304175") { Found = true });

        var nav = await sut.Navigate("SA/MAC/J.3/32");

        nav.PublicWorks.Should().BeEmpty();
        nav.WorkByIdentifiersQuery.Id.Should().Be("a9jwm2jc");
        nav.PreservationIdentifiersFromWork.Should().Equal("b18304175");
        // Both the AltRefNo (shape says CALM ref) and the b number from the work are probed; only the b number is found
        nav.StorageProbes.Where(p => p.Found).Should().ContainSingle().Which.Identifier.Should().Be("b18304175");
        nav.StorageProbes.Should().Contain(p => p.Identifier == "SA/MAC/J.3/32" && p.NotFound);
    }

    [Fact]
    public void Preservation_Identifiers_For_Work_Come_From_Locations_Then_Mets_Identifiers()
    {
        var work = new Work
        {
            Id = "w",
            Items = new[]
            {
                new Item { Locations = new[] { new Location { LocationType = new LabelledEntity { Id = "iiif-presentation" }, Url = "https://iiif.wellcomecollection.org/presentation/v2/b11111111" } } },
                new Item { Locations = new[] { new Location { LocationType = new LabelledEntity { Id = "iiif-presentation" }, Url = "https://iiif.wellcomecollection.org/presentation/PPCRI/A/1" } } },
                new Item { Locations = new[] { new Location { LocationType = new LabelledEntity { Id = "closed-stores" } } } }
            },
            Identifiers = new[]
            {
                new Identifier { IdentifierType = new LabelledEntity { Id = SourceSystems.Mets }, Value = "ppcri/a/1" },
                new Identifier { IdentifierType = new LabelledEntity { Id = SourceSystems.Mets }, Value = "b22222222" },
                new Identifier { IdentifierType = new LabelledEntity { Id = SourceSystems.SierraSystemNumber }, Value = "b11111111" }
            }
        };

        sut.GetPreservationIdentifiersForWork(work).Should().Equal("b11111111", "PPCRI/A/1", "b22222222");
    }

    [Fact]
    public async Task Predecessor_Is_The_Sierra_Sibling_Of_A_Folio_Instance()
    {
        A.CallTo(() => identifiersApi.GetSiblingsBySourceIdentifier(SourceSystems.FolioInstance, "in00067890", OntologyType.Work))
            .Returns(Set("zjytxny8",
                Row(SourceSystems.FolioInstance, "in00067890", isAlias: true),
                Row(SourceSystems.SierraSystemNumber, "b18035978")));
        A.CallTo(() => identifiersApi.GetSiblingsBySourceIdentifier(SourceSystems.FolioInstance, "in00012345", OntologyType.Work))
            .Returns(Set("abcd3456", Row(SourceSystems.FolioInstance, "in00012345")));
        A.CallTo(() => identifiersApi.GetSiblingsBySourceIdentifier(SourceSystems.FolioInstance, "in99999999", OntologyType.Work))
            .Returns((IdentifierSet) null);

        (await sut.FindPredecessorPreservationIdentifier("in00067890")).Should().Be("b18035978");
        (await sut.FindPredecessorPreservationIdentifier("in00012345")).Should().BeNull("no predecessor: post-migration item");
        (await sut.FindPredecessorPreservationIdentifier("in99999999")).Should().BeNull("unknown to the registry");
    }
}
