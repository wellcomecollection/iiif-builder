using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers;
using Wellcome.Dds.Identifiers;
using Wellcome.Dds.Repositories.Identifiers;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Identifiers;

public class WellcomeCollectionIdentifiersApiTests
{
    private const string BaseUrl = "https://api.example.org/identifiers/v1";
    private const string ApiKey = "test-key";

    private const string IdentifierSetJson =
        """
        {"canonicalId": "vsyg427x", "type": "Work", "sourceIdentifiers": [
          {"type": "Work", "sourceSystem": "axiell-guid", "value": "3c5b8da0-6237-4e98-848a-ad624b99d17c", "isAlias": true, "createdAt": "2026-09-24T12:21:01Z"},
          {"type": "Work", "sourceSystem": "calm-record-id", "value": "c460f493-294e-4043-87bc-2aa753c35f95", "isAlias": false, "createdAt": "2026-03-19T04:25:35Z"}
        ]}
        """;

    private const string NotFoundJson = """{"error": "notFound", "message": "no mapping found"}""";
    private const string BadRequestJson = """{"error": "badRequest", "message": "canonicalId does not match the required format"}""";

    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly WellcomeCollectionIdentifiersApi sut;

    public WellcomeCollectionIdentifiersApiTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var options = Options.Create(new IdentifiersApiOptions { BaseUrl = BaseUrl + "/", ApiKey = ApiKey });
        sut = new WellcomeCollectionIdentifiersApi(
            options,
            new HttpClient(httpHandler),
            NullLogger<WellcomeCollectionIdentifiersApi>.Instance);
    }

    [Fact]
    public async Task Forward_Lookup_Requests_Canonical_Url_With_Api_Key()
    {
        httpHandler.GetResponseMessage(IdentifierSetJson, HttpStatusCode.OK);
        HttpRequestMessage sent = null;
        httpHandler.RegisterCallback(r => sent = r);

        await sut.GetByCanonicalId("vsyg427x");

        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be($"{BaseUrl}/vsyg427x");
        sent.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be(ApiKey);
    }

    [Fact]
    public async Task Forward_Lookup_Parses_Identifier_Set_And_Headers()
    {
        var response = httpHandler.GetResponseMessage(IdentifierSetJson, HttpStatusCode.OK);
        response.Headers.ETag = new EntityTagHeaderValue("\"2-2026-09-24T12:21:01Z\"", isWeak: true);
        response.Headers.CacheControl = CacheControlHeaderValue.Parse("public, max-age=300");

        var set = await sut.GetByCanonicalId("vsyg427x");

        set.Should().NotBeNull();
        set.CanonicalId.Should().Be("vsyg427x");
        set.Type.Should().Be(OntologyType.Work);
        set.SourceIdentifiers.Should().HaveCount(2);
        set.SourceIdentifiers[0].SourceSystem.Should().Be("axiell-guid");
        set.SourceIdentifiers[0].IsAlias.Should().BeTrue();
        set.SourceIdentifiers[0].CreatedAt.Year.Should().Be(2026);
        set.Original.Should().NotBeNull();
        set.Original.SourceSystem.Should().Be("calm-record-id");
        set.Original.Value.Should().Be("c460f493-294e-4043-87bc-2aa753c35f95");
        set.ETag.Should().Be("W/\"2-2026-09-24T12:21:01Z\"");
        set.CacheControl.Should().Be("public, max-age=300");
    }

    [Fact]
    public async Task Forward_Lookup_Returns_Null_On_404()
    {
        httpHandler.GetResponseMessage(NotFoundJson, HttpStatusCode.NotFound);

        var set = await sut.GetByCanonicalId("zzzzzzzz");

        set.Should().BeNull();
    }

    [Fact]
    public async Task Forward_Lookup_Throws_With_Error_Body_On_400()
    {
        httpHandler.GetResponseMessage(BadRequestJson, HttpStatusCode.BadRequest);

        var act = () => sut.GetByCanonicalId("b14658197");

        var ex = (await act.Should().ThrowAsync<IdentifiersApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Url.Should().Be($"{BaseUrl}/b14658197");
        ex.Error.Error.Should().Be("badRequest");
        ex.Error.Message.Should().Be("canonicalId does not match the required format");
        ex.Message.Should().Contain("400").And.Contain("canonicalId does not match the required format");
    }

    [Fact]
    public async Task Non_Json_Error_Body_Is_Surfaced_As_Message()
    {
        httpHandler.GetResponseMessage("{\"message\":\"Forbidden\"}", HttpStatusCode.Forbidden);

        var act = () => sut.GetByCanonicalId("vsyg427x");

        var ex = (await act.Should().ThrowAsync<IdentifiersApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ex.Error.Message.Should().Be("Forbidden");
    }

    [Fact]
    public async Task Bare_Reverse_Lookup_Returns_Canonical_Id_Ref()
    {
        httpHandler.GetResponseMessage("""{"canonicalId": "nydjbrr7"}""", HttpStatusCode.OK);

        var canonicalIdRef = await sut.GetBySourceIdentifier("sierra-system-number", "b14658197");

        canonicalIdRef.Should().NotBeNull();
        canonicalIdRef.CanonicalId.Should().Be("nydjbrr7");
        httpHandler.CallsMade.Should().ContainSingle().Which.Should()
            .Be($"{BaseUrl}/by-source/sierra-system-number/b14658197?type=Work");
    }

    [Fact]
    public async Task Reverse_Lookup_Escapes_Value_As_Single_Path_Segment()
    {
        httpHandler.GetResponseMessage(NotFoundJson, HttpStatusCode.NotFound);

        await sut.GetBySourceIdentifier("calm-ref-no", "PPCRI/A/B/C");

        httpHandler.CallsMade.Should().ContainSingle().Which.Should()
            .Be($"{BaseUrl}/by-source/calm-ref-no/PPCRI%2FA%2FB%2FC?type=Work");
    }

    [Fact]
    public async Task Reverse_Lookup_With_Siblings_Adds_Include_And_Type()
    {
        httpHandler.GetResponseMessage(IdentifierSetJson, HttpStatusCode.OK);

        var set = await sut.GetSiblingsBySourceIdentifier("miro-image-number", "V0012345", OntologyType.Image);

        set.Should().NotBeNull();
        httpHandler.CallsMade.Should().ContainSingle().Which.Should()
            .Be($"{BaseUrl}/by-source/miro-image-number/V0012345?type=Image&include=siblings");
    }

    [Fact]
    public void Api_Urls_Are_Built_From_Base_Url()
    {
        sut.GetApiUrl("vsyg427x").Should().Be($"{BaseUrl}/vsyg427x");
        sut.GetApiUrl("mets", "b14658197").Should().Be($"{BaseUrl}/by-source/mets/b14658197?type=Work");
        sut.GetApiUrl("mets", "b14658197", OntologyType.Work, true).Should()
            .Be($"{BaseUrl}/by-source/mets/b14658197?type=Work&include=siblings");
    }

    [Fact]
    public async Task Resolve_Tries_Every_Candidate_And_Records_Each_Outcome()
    {
        httpHandler.GetResponseMessage(NotFoundJson, HttpStatusCode.NotFound);

        var resolution = await sut.Resolve("b1465819");

        resolution.Identifier.Should().Be("b1465819");
        resolution.AnyFound.Should().BeFalse();
        resolution.Lookups.Select(l => l.Candidate.SourceSystem).Should()
            .Equal(SourceSystems.Mets, SourceSystems.SierraSystemNumber, SourceSystems.SierraIdentifier);
        resolution.Lookups.Should().OnlyContain(l => l.Error == null && l.IdentifierSet == null);
        resolution.PreservationIdentifierLookups.Should().BeEmpty();
        httpHandler.CallsMade.Should().Equal(
            $"{BaseUrl}/by-source/mets/b14658197?type=Work&include=siblings",
            $"{BaseUrl}/by-source/sierra-system-number/b14658197?type=Work&include=siblings",
            $"{BaseUrl}/by-source/sierra-identifier/1465819?type=Work&include=siblings");
    }

    [Fact]
    public async Task Resolve_Of_Canonical_Id_Uses_Forward_Lookup()
    {
        httpHandler.GetResponseMessage(IdentifierSetJson, HttpStatusCode.OK);

        var resolution = await sut.Resolve("vsyg427x");

        resolution.AnyFound.Should().BeTrue();
        resolution.Lookups.Should().ContainSingle();
        resolution.Lookups[0].Candidate.IsCanonical.Should().BeTrue();
        resolution.Lookups[0].ApiUrl.Should().Be($"{BaseUrl}/vsyg427x");
        resolution.Found.Single().IdentifierSet.CanonicalId.Should().Be("vsyg427x");
    }

    [Fact]
    public async Task Resolve_Records_Error_Rather_Than_Throwing()
    {
        httpHandler.GetResponseMessage(BadRequestJson, HttpStatusCode.BadRequest);

        var resolution = await sut.Resolve("vsyg427x");

        resolution.AnyFound.Should().BeFalse();
        resolution.Lookups.Should().ContainSingle().Which.Error.Should().Contain("400");
    }
}
