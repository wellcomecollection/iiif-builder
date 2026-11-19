using System;
using FluentAssertions;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.DigitalObjects
{
    public class DlcsImageAccessTests
    {
        private const string Clickthrough = "https://api.dlcs.io/customers/2/roles/clickthrough";
        private const string Restricted = "https://api.dlcs.io/customers/2/roles/restricted";
        private const string Unobtainable = DlcsImageAccess.UnobtainableRole;

        private static readonly DlcsImageAccess Legacy = new(DlcsSizeModel.Legacy, 1000, false);
        private static readonly DlcsImageAccess Explicit = new(DlcsSizeModel.Explicit, 1000, false);

        // The kinds of DLCS asset the DDS registers
        public static TheoryData<string, bool, string[], bool, bool> Kinds => new()
        {
            { "open image, high-res permitted", true, Array.Empty<string>(), true, false },
            { "open image, high-res not permitted", true, Array.Empty<string>(), false, false },
            { "clickthrough image", true, new[] { Clickthrough }, false, true },
            { "restricted image", true, new[] { Restricted }, true, false },
            { "open AV or file", false, Array.Empty<string>(), false, false },
            { "restricted AV or file", false, new[] { Restricted }, false, false },
        };

        private static Image Make(DlcsImageAccess access, bool isImage, string[] roles, bool permitsHighRes,
            bool isClickthrough)
        {
            var image = new Image();
            access.Apply(image, isImage, roles, permitsHighRes, isClickthrough);
            return image;
        }

        [Theory]
        [InlineData(true, new string[0], true, false, -1)]
        [InlineData(true, new string[0], false, false, 1000)]
        [InlineData(true, new[] { Clickthrough }, false, true, 200)]
        [InlineData(true, new[] { Restricted }, true, false, 0)]
        [InlineData(false, new string[0], false, false, -1)]
        [InlineData(false, new[] { Restricted }, false, false, -1)]
        public void Legacy_sends_maxUnauthorised_as_before(bool isImage, string[] roles, bool permitsHighRes,
            bool isClickthrough, int expected)
        {
            var image = Make(Legacy, isImage, roles, permitsHighRes, isClickthrough);

            image.MaxUnauthorised.Should().Be(expected);
            image.Roles.Should().Equal(roles);
            image.MaxWidth.Should().BeNull("Legacy doesn't send it");
            image.OpenFullMax.Should().BeNull("Legacy doesn't send it");
        }

        [Theory]
        [InlineData(true, new string[0], true, false, 0, 0)]
        [InlineData(true, new string[0], false, false, 1000, 0)]
        [InlineData(true, new[] { Clickthrough }, false, true, 0, 200)]
        [InlineData(true, new[] { Restricted }, true, false, 0, 0)]
        [InlineData(false, new string[0], false, false, 0, 0)]
        [InlineData(false, new[] { Restricted }, false, false, 0, 0)]
        public void Explicit_sends_maxWidth_and_openFullMax(bool isImage, string[] roles, bool permitsHighRes,
            bool isClickthrough, int maxWidth, int openFullMax)
        {
            var image = Make(Explicit, isImage, roles, permitsHighRes, isClickthrough);

            image.MaxUnauthorised.Should().BeNull("Explicit never sends it");
            image.Roles.Should().Equal(roles, "an open image has no roles, not the unobtainable role");
            image.MaxWidth.Should().Be(maxWidth);
            image.OpenFullMax.Should().Be(openFullMax);
        }

        [Fact]
        public void Explicit_with_RestrictedDownloadMaxWidth_0_leaves_restricted_open_images_unlimited()
        {
            var optionC = new DlcsImageAccess(DlcsSizeModel.Explicit, 0, false);

            var image = Make(optionC, true, Array.Empty<string>(), false, false);

            image.MaxWidth.Should().Be(0);
            image.OpenFullMax.Should().Be(0);
            image.Roles.Should().BeEmpty();
        }

        [Fact]
        public void A_negative_RestrictedDownloadMaxWidth_is_treated_as_0()
        {
            // Protagonist stores a negative maxWidth as 0; anything else would mismatch on every sync
            var negative = new DlcsImageAccess(DlcsSizeModel.Explicit, -1, false);

            var image = Make(negative, true, Array.Empty<string>(), false, false);

            image.MaxWidth.Should().Be(0);
        }

        /// <summary>
        /// What Protagonist 1.13.1's migration (and its API conversion of maxUnauthorised) makes of a Legacy
        /// registration: see the migration "Images gains maxWidth and openFullMax".
        /// </summary>
        private static Image Migrate(Image legacy)
        {
            var mu = legacy.MaxUnauthorised!.Value;
            var hasRoles = legacy.Roles!.Length > 0;
            return new Image
            {
                MaxUnauthorised = mu,
                MaxWidth = 0,
                OpenFullMax = mu > 0 ? mu : 0,
                Roles = !hasRoles && mu >= 0 ? new[] { Unobtainable } : legacy.Roles
            };
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void Switching_to_Explicit_reingests_only_open_images_that_do_not_permit_high_res(
            string kind, bool isImage, string[] roles, bool permitsHighRes, bool isClickthrough)
        {
            // An image registered under Legacy, then migrated, then synced under Explicit
            var onDlcs = Migrate(Make(Legacy, isImage, roles, permitsHighRes, isClickthrough));
            var expected = Make(Explicit, isImage, roles, permitsHighRes, isClickthrough);

            var mismatches = Explicit.SizeMismatches(expected, onDlcs);

            if (isImage && roles.Length == 0 && !permitsHighRes)
            {
                mismatches.Should().BeEquivalentTo(new[]
                {
                    ("maxWidth", (int?)1000, (int?)0),
                    ("openFullMax", (int?)0, (int?)1000)
                }, kind);
                Explicit.RolesMatch(expected, onDlcs).Should().BeFalse("the unobtainable role goes too");
            }
            else
            {
                mismatches.Should().BeEmpty(kind);
                Explicit.RolesMatch(expected, onDlcs).Should().BeTrue(kind);
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void Legacy_finds_nothing_to_change_after_the_migration(
            string kind, bool isImage, string[] roles, bool permitsHighRes, bool isClickthrough)
        {
            var onDlcs = Migrate(Make(Legacy, isImage, roles, permitsHighRes, isClickthrough));
            var expected = Make(Legacy, isImage, roles, permitsHighRes, isClickthrough);

            Legacy.SizeMismatches(expected, onDlcs).Should().BeEmpty(kind);
            Legacy.RolesMatch(expected, onDlcs).Should().BeTrue(kind);
        }

        [Fact]
        public void Legacy_still_removes_the_unobtainable_role_from_an_image_that_should_be_fully_open()
        {
            var expected = Make(Legacy, true, Array.Empty<string>(), true, false);
            var onDlcs = new Image { MaxUnauthorised = -1, Roles = new[] { Unobtainable } };

            Legacy.RolesMatch(expected, onDlcs).Should().BeFalse("maxUnauthorised -1 means no limit and no role");
        }

        [Fact]
        public void RolesMatch_treats_null_and_empty_alike()
        {
            Legacy.RolesMatch(new Image { Roles = null }, new Image { Roles = Array.Empty<string>() })
                .Should().BeTrue();
            Explicit.RolesMatch(new Image { Roles = Array.Empty<string>() }, new Image { Roles = null })
                .Should().BeTrue();
        }

        [Fact]
        public void Explicit_treats_missing_size_fields_as_0()
        {
            var expected = Make(Explicit, true, Array.Empty<string>(), true, false);

            Explicit.SizeMismatches(expected, new Image { MaxWidth = null, OpenFullMax = null })
                .Should().BeEmpty();
        }

        [Fact]
        public void Hold_does_nothing_when_off()
        {
            var expected = Make(Explicit, true, Array.Empty<string>(), false, false);
            var onDlcs = new Image { MaxWidth = 0, OpenFullMax = 1000, Roles = new[] { Unobtainable } };

            Explicit.Hold(expected, onDlcs).Should().BeEmpty();
            expected.MaxWidth.Should().Be(1000);
            expected.Roles.Should().BeEmpty();
        }

        [Fact]
        public void Hold_keeps_the_existing_roles_and_size_fields_and_says_so()
        {
            var holding = new DlcsImageAccess(DlcsSizeModel.Explicit, 1000, true);
            var expected = Make(holding, true, Array.Empty<string>(), false, false);
            var onDlcs = new Image { MaxWidth = 0, OpenFullMax = 1000, Roles = new[] { Unobtainable } };

            var held = holding.Hold(expected, onDlcs);

            held.Should().HaveCount(3).And.OnlyContain(m => m.StartsWith("HELD (Dlcs:HoldAccessChanges)"));
            expected.Roles.Should().Equal(Unobtainable);
            expected.MaxWidth.Should().Be(0);
            expected.OpenFullMax.Should().Be(1000);
            holding.SizeMismatches(expected, onDlcs).Should().BeEmpty();
            holding.RolesMatch(expected, onDlcs).Should().BeTrue();
        }

        [Fact]
        public void Hold_under_Legacy_keeps_maxUnauthorised()
        {
            var holding = new DlcsImageAccess(DlcsSizeModel.Legacy, 1000, true);
            var expected = Make(holding, true, Array.Empty<string>(), true, false);
            var onDlcs = new Image { MaxUnauthorised = 1000, Roles = new[] { Unobtainable } };

            var held = holding.Hold(expected, onDlcs);

            held.Should().HaveCount(2, "roles and maxUnauthorised");
            expected.MaxUnauthorised.Should().Be(1000);
            expected.Roles.Should().Equal(Unobtainable);
        }

        [Fact]
        public void Hold_leaves_an_image_alone_when_nothing_differs()
        {
            var holding = new DlcsImageAccess(DlcsSizeModel.Legacy, 1000, true);
            var expected = Make(holding, true, new[] { Clickthrough }, false, true);
            var onDlcs = new Image { MaxUnauthorised = 200, Roles = new[] { Clickthrough } };

            holding.Hold(expected, onDlcs).Should().BeEmpty();
        }

        [Fact]
        public void DlcsLacksSizeFields_only_under_Explicit_and_only_when_openFullMax_is_missing()
        {
            var old = new[] { new Image { MaxUnauthorised = -1 } };
            var current = new[] { new Image { MaxUnauthorised = -1, MaxWidth = 0, OpenFullMax = 0 } };

            Legacy.DlcsLacksSizeFields(old).Should().BeFalse();
            Explicit.DlcsLacksSizeFields(old).Should().BeTrue();
            Explicit.DlcsLacksSizeFields(current).Should().BeFalse();
            Explicit.DlcsLacksSizeFields(Array.Empty<Image>()).Should().BeFalse("nothing to tell from");
        }

        [Theory]
        [InlineData(DlcsSizeModel.Explicit, 1000, true, 0)]
        [InlineData(DlcsSizeModel.Explicit, 1000, false, 1000)]
        [InlineData(DlcsSizeModel.Explicit, 0, false, 0)]
        [InlineData(DlcsSizeModel.Explicit, 1500, false, 1500)]
        [InlineData(DlcsSizeModel.Legacy, 1500, false, 1000)]
        [InlineData(DlcsSizeModel.Legacy, 1000, true, 0)]
        public void OpenImageMaxSize_matches_the_limit_Apply_sends(DlcsSizeModel model, int restrictedDownloadMaxWidth,
            bool permitsHighRes, int expected)
        {
            var access = new DlcsImageAccess(model, restrictedDownloadMaxWidth, false);

            access.OpenImageMaxSize(permitsHighRes).Should().Be(expected);

            var image = Make(access, true, Array.Empty<string>(), permitsHighRes, false);
            var sent = model == DlcsSizeModel.Legacy ? image.MaxUnauthorised : image.MaxWidth;
            Math.Max(0, sent!.Value).Should().Be(expected, "it's the limit the DLCS is given for the image");
        }
    }
}
