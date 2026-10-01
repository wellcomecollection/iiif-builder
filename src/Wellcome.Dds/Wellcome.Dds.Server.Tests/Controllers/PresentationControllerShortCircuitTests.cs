using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Server.Controllers;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Controllers
{
    public class PresentationControllerShortCircuitTests
    {
        private const string PresentationRoot = "https://test.linkeddata/presentation/";

        private static readonly UriPatterns UriPatterns = new(Options.Create(new DdsOptions
        {
            LinkedDataDomain = "https://test.linkeddata",
            WellcomeCollectionApi = "https://test.wellcomeapi",
            ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
        }));

        private readonly IIdentityService identityService;
        private readonly PresentationController sut;

        public PresentationControllerShortCircuitTests()
        {
            identityService = A.Fake<IIdentityService>();
            A.CallTo(() => identityService.GetIdentity(A<string>._))
                .Throws(new FormatException("Not a valid identifier"));
            // Dependencies used only after the short-circuit are left null
            sut = new PresentationController(
                new NullLogger<PresentationController>(),
                Options.Create(new DdsOptions()),
                null,
                UriPatterns,
                null,
                A.Fake<IIIIFBuilder>(),
                A.Fake<ICatalogue>(),
                null,
                identityService)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public static IEnumerable<object[]> MintedNonDereferenceableResources()
        {
            foreach (var manifest in new[] { "b13248169", "b19974760_233_0001", "PPCRI/A/1" })
            {
                var asset = manifest.Replace('/', '_') + "_0001.jp2";
                yield return new object[] { UriPatterns.Canvas(manifest, asset) };
                yield return new object[] { UriPatterns.CanvasPaintingAnnotationPage(manifest, asset) };
                yield return new object[] { UriPatterns.CanvasPaintingAnnotation(manifest, asset) };
                yield return new object[] { UriPatterns.CanvasSupplementingAnnotationPage(manifest, asset) };
                yield return new object[] { UriPatterns.CanvasSupplementingAnnotation(manifest, asset, "pdf") };
                yield return new object[] { UriPatterns.CanvasClassifyingAnnotation(manifest, asset, "a1") };
                yield return new object[] { UriPatterns.Range(manifest, "LOG_0001") };
            }
            // Born-digital range ids are folder paths, so they can contain slashes
            yield return new object[] { UriPatterns.Range("PPFDN/E/2", "objects") };
            yield return new object[] { UriPatterns.Range("PPFDN/E/2", "FD_PHOTOS_ON_CD") };
            yield return new object[] { UriPatterns.Range("PPFDN/E/2", "FD_PHOTOS_ON_CD/sub/deeper") };
        }

        [Theory]
        [MemberData(nameof(MintedNonDereferenceableResources))]
        public async Task Index_Returns404_WithoutResolvingIdentity_ForNonDereferenceableResources(string uri)
        {
            var id = uri.Replace(PresentationRoot, "");

            var result = await sut.Index(id);

            result.Should().BeOfType<NotFoundObjectResult>();
            sut.Response.Headers.CacheControl.ToString().Should().Be("public, s-maxage=2592000, max-age=2592000");
            A.CallTo(() => identityService.GetIdentity(A<string>._)).MustNotHaveHappened();
        }

        [Theory]
        [InlineData("b13248169")]
        [InlineData("b19974760_233_0001")]
        [InlineData("PPCRI/A/1")]
        [InlineData("PPCRI_A_1")]
        [InlineData("b13248169/canvases")]
        [InlineData("b13248169/canvases/a/b")]
        [InlineData("b13248169/canvases/a.jp2/painting/other")]
        [InlineData("b13248169/ranges")]
        [InlineData("b13248169/ranges/")]
        public async Task Index_ResolvesIdentity_ForOtherPaths(string id)
        {
            await sut.Index(id);

            A.CallTo(() => identityService.GetIdentity(id)).MustHaveHappenedOnceExactly();
            sut.Response.Headers.CacheControl.Should().BeEmpty();
        }
    }
}
