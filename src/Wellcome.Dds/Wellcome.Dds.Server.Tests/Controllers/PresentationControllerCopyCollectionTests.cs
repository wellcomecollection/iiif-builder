using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Utils.Storage;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Server.Controllers;
using Wellcome.Dds.Server.Conneg;
using Xunit;

#nullable enable

namespace Wellcome.Dds.Server.Tests.Controllers
{
    /// <summary>
    /// Copy Collections ("{work}-copy-{n}", minted by MultiCopyState) are only stored embedded in their
    /// work's Collection; the controller serves them by extracting them from it.
    /// </summary>
    public class PresentationControllerCopyCollectionTests
    {
        private const string Container = "presentation";
        private const string Root = "https://iiif.wellcomecollection.org/presentation";

        // The shape of b30413552 as stored: one copy with two volumes
        private const string StoredV3 = """
            {
              "@context": "http://iiif.io/api/presentation/3/context.json",
              "id": "https://iiif.wellcomecollection.org/presentation/b30413552",
              "type": "Collection",
              "label": { "en": [ "A political survey of Britain" ] },
              "behavior": [ "multi-part" ],
              "rights": "http://creativecommons.org/publicdomain/mark/1.0/",
              "items": [
                {
                  "id": "https://iiif.wellcomecollection.org/presentation/b30413552-copy-1",
                  "type": "Collection",
                  "label": { "en": [ "Copy 1" ] },
                  "thumbnail": [ { "id": "https://iiif.wellcomecollection.org/thumbs/b30413552_0001_0009.jp2/full/69,100/0/default.jpg", "type": "Image" } ],
                  "items": [
                    { "id": "https://iiif.wellcomecollection.org/presentation/b30413552_0001", "type": "Manifest", "label": { "en": [ "Copy 1, Volume 1" ] } },
                    { "id": "https://iiif.wellcomecollection.org/presentation/b30413552_0002", "type": "Manifest", "label": { "en": [ "Copy 1, Volume 2" ] } }
                  ]
                },
                {
                  "id": "https://iiif.wellcomecollection.org/presentation/b30413552-copy-11",
                  "type": "Collection",
                  "label": { "en": [ "Copy 11" ] },
                  "items": [
                    { "id": "https://iiif.wellcomecollection.org/presentation/b30413552_0003", "type": "Manifest", "label": { "en": [ "Copy 11, Volume 1" ] } }
                  ]
                }
              ]
            }
            """;

        private const string StoredV2 = """
            {
              "@context": "http://iiif.io/api/presentation/2/context.json",
              "@id": "https://iiif.wellcomecollection.org/presentation/v2/b30413552",
              "@type": "sc:Collection",
              "label": "A political survey of Britain",
              "collections": [
                {
                  "@id": "https://iiif.wellcomecollection.org/presentation/v2/b30413552-copy-1",
                  "@type": "sc:Collection",
                  "label": "Copy 1",
                  "within": "https://iiif.wellcomecollection.org/presentation/v2/b30413552",
                  "manifests": [
                    { "@id": "https://iiif.wellcomecollection.org/presentation/v2/b30413552_0001", "@type": "sc:Manifest", "label": "Copy 1, Volume 1" },
                    { "@id": "https://iiif.wellcomecollection.org/presentation/v2/b30413552_0002", "@type": "sc:Manifest", "label": "Copy 1, Volume 2" }
                  ]
                }
              ]
            }
            """;

        private readonly IStorage storage = A.Fake<IStorage>();
        private readonly IIdentityService identityService = A.Fake<IIdentityService>();
        private readonly PresentationController sut;

        public PresentationControllerCopyCollectionTests()
        {
            A.CallTo(() => storage.GetStream(A<string>._, A<string>._)).Returns(Task.FromResult<Stream?>(null));
            Store("v3/b30413552", StoredV3);
            Store("v2/b30413552", StoredV2);
            A.CallTo(() => identityService.GetIdentity(A<string>._))
                .ReturnsLazily((string s) => ParsingIdentityService.Parse(s));

            var options = Options.Create(new DdsOptions
            {
                PresentationContainer = Container,
                LinkedDataDomain = "https://iiif.wellcomecollection.org",
                WellcomeCollectionApi = "https://test.wellcomeapi",
                ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
            });
            var linkRewriter = new LinkRewriter(options);
            sut = new PresentationController(
                new NullLogger<PresentationController>(),
                options,
                new Helpers(storage, linkRewriter),
                new UriPatterns(options),
                null,
                A.Fake<IIIIFBuilder>(),
                A.Fake<ICatalogue>(),
                linkRewriter,
                identityService)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private void Store(string path, string json) =>
            A.CallTo(() => storage.GetStream(Container, path))
                .ReturnsLazily(() => Task.FromResult<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes(json))));

        private static JObject Json(IActionResult result, string contentType)
        {
            var content = result.Should().BeOfType<ContentResult>().Subject;
            // ASP.NET re-serialises the header (";profile=" becomes "; profile="), so compare it parsed
            MediaTypeHeaderValue.Parse(content.ContentType).Should().Be(MediaTypeHeaderValue.Parse(contentType));
            return JObject.Parse(content.Content!);
        }

        [Fact]
        public async Task Canonical_path_serves_the_copy_as_IIIF_3_by_default()
        {
            var json = Json(await sut.Index("b30413552-copy-1"), IIIFPresentation.ContentTypes.V3);

            json["@context"]!.Value<string>().Should().Be("http://iiif.io/api/presentation/3/context.json");
            json["id"]!.Value<string>().Should().Be($"{Root}/b30413552-copy-1");
            json["type"]!.Value<string>().Should().Be("Collection");
            json["label"]!["en"]![0]!.Value<string>().Should().Be("Copy 1");
            json["thumbnail"].Should().NotBeNull();
            json["items"]!.Select(i => i["id"]!.Value<string>()).Should().Equal(
                $"{Root}/b30413552_0001", $"{Root}/b30413552_0002");
            json.Properties().First().Name.Should().Be("@context");
        }

        [Fact]
        public async Task The_IIIF_3_copy_is_partOf_its_work_and_takes_none_of_its_other_properties()
        {
            var json = Json(await sut.V3("b30413552-copy-1"), IIIFPresentation.ContentTypes.V3);

            var partOf = json["partOf"]!.Should().ContainSingle().Subject;
            partOf["id"]!.Value<string>().Should().Be($"{Root}/b30413552");
            partOf["type"]!.Value<string>().Should().Be("Collection");
            partOf["label"]!["en"]![0]!.Value<string>().Should().Be("A political survey of Britain");
            json["rights"].Should().BeNull();
            json["behavior"].Should().BeNull();
        }

        [Fact]
        public async Task The_v2_path_serves_the_copy_from_the_IIIF_2_collections()
        {
            var json = Json(await sut.V2("b30413552-copy-1"), IIIFPresentation.ContentTypes.V2);

            json["@context"]!.Value<string>().Should().Be("http://iiif.io/api/presentation/2/context.json");
            json["@id"]!.Value<string>().Should().Be($"{Root}/v2/b30413552-copy-1");
            json["within"]!.Value<string>().Should().Be($"{Root}/v2/b30413552");
            json["manifests"]!.Should().HaveCount(2);
            json["partOf"].Should().BeNull();
        }

        [Fact]
        public async Task Canonical_path_honours_an_Accept_header_for_IIIF_2()
        {
            sut.ControllerContext.HttpContext.Request.Headers.Accept = IIIFPresentation.ContentTypes.V2;

            var json = Json(await sut.Index("b30413552-copy-1"), IIIFPresentation.ContentTypes.V2);

            json["@id"]!.Value<string>().Should().Be($"{Root}/v2/b30413552-copy-1");
        }

        [Fact]
        public async Task Copy_numbers_match_exactly()
        {
            var json = Json(await sut.V3("b30413552-copy-11"), IIIFPresentation.ContentTypes.V3);

            json["id"]!.Value<string>().Should().Be($"{Root}/b30413552-copy-11");
            json["items"]!.Should().ContainSingle();
        }

        [Fact]
        public async Task A_trailing_slash_is_ignored()
        {
            var json = Json(await sut.Index("b30413552-copy-1/"), IIIFPresentation.ContentTypes.V3);

            json["id"]!.Value<string>().Should().Be($"{Root}/b30413552-copy-1");
        }

        [Theory]
        [InlineData("b30413552-copy-2")]  // the work has no copy 2
        [InlineData("b30413552-copy-01")] // not how copy ids are minted
        [InlineData("b10727000-copy-1")]  // the work isn't stored
        public async Task A_copy_that_does_not_exist_is_404(string id)
        {
            (await sut.Index(id)).Should().BeOfType<NotFoundObjectResult>();
            (await sut.V3(id)).Should().BeOfType<NotFoundObjectResult>();
            (await sut.V2(id)).Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task An_invalid_work_identifier_is_404()
        {
            A.CallTo(() => identityService.GetIdentity("not-an-id"))
                .Throws(new FormatException("Not a valid identifier"));

            (await sut.Index("not-an-id-copy-1")).Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Only_the_work_identifier_is_resolved()
        {
            await sut.Index("b30413552-copy-1");

            A.CallTo(() => identityService.GetIdentity("b30413552")).MustHaveHappenedOnceExactly();
            A.CallTo(() => identityService.GetIdentity("b30413552-copy-1")).MustNotHaveHappened();
        }
    }
}
