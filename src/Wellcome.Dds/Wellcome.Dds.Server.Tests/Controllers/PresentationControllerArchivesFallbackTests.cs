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

#nullable enable

namespace Wellcome.Dds.Server.Tests.Controllers
{
    /// <summary>
    /// The archives routes, and the presentation path's fallback when nothing is stored, redirect a work
    /// with a IIIF digital location to its Manifest. The b-number they redirect to must be the digitised one.
    /// </summary>
    public class PresentationControllerArchivesFallbackTests
    {
        private const string Root = "https://test.linkeddata/presentation";

        private readonly ICatalogue catalogue = A.Fake<ICatalogue>();
        private readonly PresentationController sut;

        public PresentationControllerArchivesFallbackTests()
        {
            var identityService = A.Fake<IIdentityService>();
            A.CallTo(() => identityService.GetIdentity(A<string>._))
                .ReturnsLazily((string s) => ParsingIdentityService.Parse(s));
            var options = Options.Create(new DdsOptions
            {
                LinkedDataDomain = "https://test.linkeddata",
                WellcomeCollectionApi = "https://test.wellcomeapi",
                ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
            });
            sut = new PresentationController(
                new NullLogger<PresentationController>(),
                options,
                null,
                new UriPatterns(options),
                null,
                A.Fake<IIIIFBuilder>(),
                catalogue,
                null,
                identityService)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private static Work WorkWith(string iiifLocation, params string[] sierraBNumbers)
        {
            var identifiers = new Identifier[sierraBNumbers.Length];
            for (var i = 0; i < sierraBNumbers.Length; i++)
            {
                identifiers[i] = new Identifier
                {
                    IdentifierType = new LabelledEntity { Id = "sierra-system-number" },
                    Value = sierraBNumbers[i]
                };
            }
            return new Work
            {
                Id = "nadgt6ed",
                Identifiers = identifiers,
                Items = new[]
                {
                    new Item
                    {
                        Locations = new[]
                        {
                            new Location
                            {
                                LocationType = new LabelledEntity { Id = "iiif-presentation" },
                                Url = iiifLocation
                            }
                        }
                    }
                }
            };
        }

        // nadgt6ed: merged from the print record b12705251 and the e-record b30413552, print listed first
        private void GivenAMergedPrintAndERecord(string requested) =>
            A.CallTo(() => catalogue.GetWorkByOtherIdentifier(requested)).Returns(WorkWith(
                "https://iiif.wellcomecollection.org/presentation/v2/b30413552", "b12705251", "b30413552"));

        [Fact]
        public async Task The_digitised_b_number_is_not_redirected_to_the_print_record()
        {
            GivenAMergedPrintAndERecord("b30413552");

            var result = await sut.ArchivesReference("b30413552", fromPresentationPath: true);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task The_print_record_redirects_to_the_digitised_b_number()
        {
            GivenAMergedPrintAndERecord("b12705251");

            var result = await sut.ArchivesReference("b12705251", fromPresentationPath: true);

            result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be($"{Root}/b30413552");
        }

        [Fact]
        public async Task The_IIIF_2_route_redirects_to_the_digitised_b_number()
        {
            GivenAMergedPrintAndERecord("b12705251");

            var result = await sut.ArchivesReferenceV2("b12705251");

            result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be($"{Root}/v2/b30413552");
        }

        [Fact]
        public async Task A_location_that_names_no_b_number_falls_back_to_the_Sierra_b_number()
        {
            A.CallTo(() => catalogue.GetWorkByOtherIdentifier("PPCRI/A/1")).Returns(WorkWith(
                "https://iiif.wellcomecollection.org/presentation/PPCRI_A_1", "b16784613"));

            var result = await sut.ArchivesReference("PPCRI/A/1", fromPresentationPath: true);

            result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be($"{Root}/b16784613");
        }

        [Fact]
        public async Task The_IIIF_2_route_is_404_when_the_work_has_no_b_number()
        {
            A.CallTo(() => catalogue.GetWorkByOtherIdentifier("PPCRI/A/1")).Returns(WorkWith(
                "https://iiif.wellcomecollection.org/presentation/PPCRI_A_1"));

            var result = await sut.ArchivesReferenceV2("PPCRI/A/1");

            result.Should().BeOfType<NotFoundResult>();
        }
    }
}
