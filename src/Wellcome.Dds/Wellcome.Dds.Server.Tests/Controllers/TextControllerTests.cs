using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utils.Storage;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Server.Controllers;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Controllers
{
    public class TextControllerTests
    {
        private readonly IIdentityService identityService;
        private readonly IMetsRepository metsRepository;
        private readonly TextController sut;

        public TextControllerTests()
        {
            identityService = A.Fake<IIdentityService>();
            metsRepository = A.Fake<IMetsRepository>();
            sut = new TextController(
                A.Fake<IStorage>(),
                Options.Create(new DdsOptions()),
                metsRepository,
                new NullLogger<TextController>(),
                identityService);
        }

        [Fact]
        public async Task Alto_Returns404_WhenIdentifierCannotBeParsed()
        {
            // Only malformed b numbers fail to parse (here, a wrong check digit); anything that
            // isn't a b number parses as a CALM ref - see the PackageNotInStorage case.
            A.CallTo(() => identityService.GetIdentity("b99999999"))
                .Throws(new FormatException("Not a valid identifier"));

            var result = await sut.Alto("b99999999", "b99999999_0001.jp2");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Theory]
        [InlineData("zzzzzzzz")]  // catalogue work id shape; parses as a CALM ref
        [InlineData("b99999997")] // valid check digit, no such package
        public async Task Alto_Returns404_WhenPackageNotInStorage(string identifier)
        {
            var ddsId = MakeIdentity(identifier);
            A.CallTo(() => identityService.GetIdentity(identifier)).Returns(ddsId);
            A.CallTo(() => metsRepository.GetAsync(ddsId))
                .Throws(new HttpRequestException("Response status code does not indicate success: 404 (Not Found).",
                    null, HttpStatusCode.NotFound));

            var result = await sut.Alto(identifier, $"{identifier}_0001.jp2");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Alto_Returns404_WhenStorageMapIsNull()
        {
            var ddsId = MakeIdentity("b99999997");
            A.CallTo(() => identityService.GetIdentity("b99999997")).Returns(ddsId);
            A.CallTo(() => metsRepository.GetAsync(ddsId))
                .Throws(new InvalidOperationException("Could not retrieve storage map for b99999997"));

            var result = await sut.Alto("b99999997", "x.jp2");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Alto_Throws_WhenStorageServiceFails()
        {
            // A storage service outage is a real 5xx, not a missing resource
            var ddsId = MakeIdentity("b30309700");
            A.CallTo(() => identityService.GetIdentity("b30309700")).Returns(ddsId);
            A.CallTo(() => metsRepository.GetAsync(ddsId))
                .Throws(new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable));

            var action = () => sut.Alto("b30309700", "b30309700_0001.jp2");

            await action.Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task Alto_Returns404_WhenVolumeMetsDoesNotExist()
        {
            // An asset filename in the manifestation slot parses as a volume identifier,
            // whose METS file is then not present in the (healthy) package.
            var ddsId = MakeIdentity("b31492290_0001.jp2");
            A.CallTo(() => identityService.GetIdentity("b31492290_0001.jp2")).Returns(ddsId);
            A.CallTo(() => metsRepository.GetAsync(ddsId))
                .Throws(new FileNotFoundException("File not present in storage map: b31492290_0001.jp2.xml"));

            var result = await sut.Alto("b31492290_0001.jp2", "b31492290_0001.jp2");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Alto_Returns404_WhenAssetNotInManifestation()
        {
            var ddsId = MakeIdentity("b31492290");
            A.CallTo(() => identityService.GetIdentity("b31492290")).Returns(ddsId);
            var manifestation = A.Fake<IManifestation>();
            A.CallTo(() => manifestation.Sequence).Returns(new List<IPhysicalFile>());
            A.CallTo(() => metsRepository.GetAsync(ddsId)).Returns(manifestation);

            var result = await sut.Alto("b31492290", "nope.jp2");

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        private static DdsIdentity MakeIdentity(string value) =>
            new()
            {
                Value = value,
                LowerCaseValue = value.ToLowerInvariant(),
                PackageIdentifier = "b31492290",
                PackageIdentifierPathElementSafe = "b31492290",
                PathElementSafe = value,
                Source = Source.Sierra,
                Level = IdentifierLevel.Package
            };
    }
}
