using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Xunit;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.DigitalObjects
{
    /// <summary>
    /// What a sync works out for one open image, under each Dlcs:SizeModel, when the DLCS holds what
    /// Protagonist 1.13.1's migration made of the DDS's Legacy registration.
    /// </summary>
    public class SyncOperationSizeModelTests
    {
        private const string Unobtainable = DlcsImageAccess.UnobtainableRole;
        private const string StorageId = "b12345672_0001.jp2";

        private static DigitalObjectRepository MakeSut(IDlcs dlcs)
        {
            var ddsOptions = Options.Create(new DdsOptions
            {
                LinkedDataDomain = "https://test.com",
                WellcomeCollectionApi = "(unused in this test)",
                ApiWorkTemplate = "(unused in this test)"
            });
            var context = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseInMemoryDatabase(nameof(SyncOperationSizeModelTests)).Options,
                new NullLogger<DdsInstrumentationContext>());
            return new DigitalObjectRepository(new NullLogger<DigitalObjectRepository>(), new UriPatterns(ddsOptions),
                dlcs, A.Fake<IMetsRepository>(), context, ddsOptions,
                new ParsingIdentityService(new MemoryCache(new MemoryCacheOptions())));
        }

        private static IDlcs MakeDlcs(DlcsSizeModel sizeModel, bool hold = false)
        {
            var dlcs = A.Fake<IDlcs>();
            A.CallTo(() => dlcs.DefaultSpace).Returns(5);
            A.CallTo(() => dlcs.SizeModel).Returns(sizeModel);
            A.CallTo(() => dlcs.RestrictedDownloadMaxWidth).Returns(1000);
            A.CallTo(() => dlcs.HoldAccessChanges).Returns(hold);
            A.CallTo(() => dlcs.GetRoleUri(AccessCondition.RequiresRegistration))
                .Returns("https://api.dlcs.io/customers/2/roles/clickthrough");
            return dlcs;
        }

        /// <summary>An open book with one image, with or without wholeImageHighResAsJpg</summary>
        private static IDigitalManifestation MakeManifestation(bool permitsHighRes, Image onDlcs)
        {
            var physicalFile = A.Fake<IPhysicalFile>();
            A.CallTo(() => physicalFile.AccessCondition).Returns(AccessCondition.Open);
            A.CallTo(() => physicalFile.Index).Returns(0);

            var processing = A.Fake<IProcessingBehaviour>();
            A.CallTo(() => processing.AssetFamily).Returns(AssetFamily.Image);
            A.CallTo(() => processing.DeliveryChannels).Returns(new HashSet<DeliveryChannel>());

            var fileInfo = A.Fake<IArchiveStorageStoredFileInfo>();
            A.CallTo(() => fileInfo.Uri).Returns("s3://bucket/b12345672/b12345672_0001.jp2");

            var storedFile = A.Fake<IStoredFile>();
            A.CallTo(() => storedFile.StorageIdentifier).Returns(StorageId);
            A.CallTo(() => storedFile.MimeType).Returns("image/jp2");
            A.CallTo(() => storedFile.PhysicalFile).Returns(physicalFile);
            A.CallTo(() => storedFile.ProcessingBehaviour).Returns(processing);
            A.CallTo(() => storedFile.GetStoredFileInfo()).Returns(fileInfo);

            var mets = A.Fake<IManifestation>();
            A.CallTo(() => mets.Identifier).Returns("b12345672");
            A.CallTo(() => mets.SynchronisableFiles).Returns(new List<IStoredFile> { storedFile });
            A.CallTo(() => mets.IgnoredStorageIdentifiers).Returns(new List<string>());
            A.CallTo(() => mets.PermittedOperations).Returns(permitsHighRes
                ? new[] { "wholeImageHighResAsJpg" }
                : new[] { "embed" });

            return new DigitalManifestation(mets) { DlcsImages = new[] { onDlcs } };
        }

        /// <summary>
        /// The DDS's Legacy registration of an open image that doesn't permit high-res (maxUnauthorised 1000),
        /// after the 1.13.1 migration
        /// </summary>
        private static Image MigratedRestrictedOpenImage() => new()
        {
            StorageIdentifier = StorageId,
            ModelId = StorageId,
            Origin = "s3://bucket/b12345672/b12345672_0001.jp2",
            String1 = "b12345672",
            String2 = "b12345672",
            String3 = "b12345672",
            MediaType = "image/jp2",
            MaxUnauthorised = 1000,
            MaxWidth = 0,
            OpenFullMax = 1000,
            Roles = new[] { Unobtainable },
            DeliveryChannels = Array.Empty<HydraDeliveryChannel>()
        };

        private static Task<SyncOperation> Sync(IDlcs dlcs, bool permitsHighRes, Image onDlcs)
            => MakeSut(dlcs).GetDlcsSyncOperation(MakeManifestation(permitsHighRes, onDlcs), false,
                new DlcsCallContext("[test]", "[id]"));

        [Fact]
        public async Task Legacy_does_not_remove_the_unobtainable_role()
        {
            var sync = await Sync(MakeDlcs(DlcsSizeModel.Legacy), false, MigratedRestrictedOpenImage());

            sync.RequiresSync.Should().BeFalse(string.Join("; ", sync.Mismatches!.SelectMany(m => m.Value)));
            sync.DlcsImagesToPatch.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Legacy_keeps_the_unobtainable_role_when_patching_for_another_reason(bool hold)
        {
            // e.g. a page reorder or the Goobi volume-id remediation changes string1
            var onDlcs = MigratedRestrictedOpenImage();
            onDlcs.String1 = "b99999999";

            var sync = await Sync(MakeDlcs(DlcsSizeModel.Legacy, hold), false, onDlcs);

            sync.DlcsImagesToIngest.Should().BeEmpty();
            var patch = sync.DlcsImagesToPatch.Should().ContainSingle().Subject;
            patch.String1.Should().Be("b12345672");
            patch.Roles.Should().Equal(new[] { Unobtainable },
                "Protagonist 1.13.1+ applies \"roles\": [] from a PATCH, which would open the image fully");
        }

        [Fact]
        public async Task Legacy_reingests_an_image_that_should_now_be_fully_open()
        {
            // e.g. a licence name the DDS didn't recognise before #314
            var sync = await Sync(MakeDlcs(DlcsSizeModel.Legacy), true, MigratedRestrictedOpenImage());

            var image = sync.DlcsImagesToIngest.Should().ContainSingle().Subject;
            image.MaxUnauthorised.Should().Be(-1);
            image.Roles.Should().BeEmpty();
            sync.Mismatches![StorageId].Should().Contain(m => m.Contains("maxUnauthorised"));
        }

        [Fact]
        public async Task Explicit_reingests_with_no_role_and_maxWidth()
        {
            var sync = await Sync(MakeDlcs(DlcsSizeModel.Explicit), false, MigratedRestrictedOpenImage());

            var image = sync.DlcsImagesToIngest.Should().ContainSingle().Subject;
            image.MaxUnauthorised.Should().BeNull();
            image.MaxWidth.Should().Be(1000);
            image.OpenFullMax.Should().Be(0);
            image.Roles.Should().BeEmpty();
            sync.DlcsImagesToPatch.Should().BeEmpty("the registration carries the roles too");
        }

        [Fact]
        public async Task HoldAccessChanges_lists_the_changes_but_sends_nothing()
        {
            var sync = await Sync(MakeDlcs(DlcsSizeModel.Explicit, hold: true), false, MigratedRestrictedOpenImage());

            sync.RequiresSync.Should().BeFalse();
            sync.Mismatches![StorageId].Should().HaveCount(3)
                .And.OnlyContain(m => m.StartsWith("HELD (Dlcs:HoldAccessChanges)"));
        }

        [Fact]
        public async Task HoldAccessChanges_keeps_access_when_an_image_is_reingested_for_another_reason()
        {
            var onDlcs = MigratedRestrictedOpenImage();
            onDlcs.Origin = "s3://bucket/somewhere-else.jp2";

            var sync = await Sync(MakeDlcs(DlcsSizeModel.Explicit, hold: true), false, onDlcs);

            var image = sync.DlcsImagesToIngest.Should().ContainSingle().Subject;
            image.Origin.Should().Be("s3://bucket/b12345672/b12345672_0001.jp2");
            image.Roles.Should().Equal(Unobtainable);
            image.OpenFullMax.Should().Be(1000);
            image.MaxWidth.Should().Be(0);
        }

        [Fact]
        public async Task Explicit_refuses_a_DLCS_that_does_not_return_openFullMax()
        {
            var onDlcs = MigratedRestrictedOpenImage();
            onDlcs.Roles = Array.Empty<string>();
            onDlcs.MaxWidth = null;
            onDlcs.OpenFullMax = null; // as from Protagonist 1.12

            var sync = await Sync(MakeDlcs(DlcsSizeModel.Explicit), false, onDlcs);

            sync.DlcsLacksSizeFields.Should().BeTrue();
            sync.Message.Should().Contain("older than Protagonist 1.13.1");
        }
    }
}
