using System.Linq;
using FluentAssertions;
using IIIF;
using Wellcome.Dds.Repositories.Presentation;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Presentation
{
    public class ImageApiSizeHelperTests
    {
        // b33563755_0001, an open image whose METS doesn't permit high-res
        private static readonly Size Portrait = new(6120, 7447);

        [Fact]
        public void An_unlimited_open_image_gets_every_size()
        {
            var sizes = ImageApiSizeHelper.GetPossibleSizes(Portrait, true);

            sizes.Select(s => s.Width).Should().Equal(1338, 880, 842, 420, 329, 282, 164, 82);
        }

        [Fact]
        public void A_size_limited_image_gets_only_the_sizes_the_DLCS_makes()
        {
            // The DLCS's thumbnail info.json for b33563755_0001 under a 1000px maxWidth lists exactly these:
            // 880 wide is 1071 tall, and !1024,1024 is 842 x 1024, so both are over the limit
            var sizes = ImageApiSizeHelper.GetPossibleSizes(Portrait, true, 1000);

            sizes.Select(s => (s.Width, s.Height)).Should().Equal(
                (420, 511), (329, 400), (282, 343), (164, 200), (82, 100));
        }

        [Fact]
        public void The_limit_applies_to_the_longer_side()
        {
            var sizes = ImageApiSizeHelper.GetPossibleSizes(new Size(7447, 6120), true, 1000);

            sizes.Should().OnlyContain(s => s.Width <= 1000 && s.Height <= 1000);
            sizes.First().Width.Should().Be(880);
        }

        [Fact]
        public void A_limit_larger_than_every_size_changes_nothing()
        {
            ImageApiSizeHelper.GetPossibleSizes(Portrait, true, 10000).Select(s => (s.Width, s.Height))
                .Should().Equal(ImageApiSizeHelper.GetPossibleSizes(Portrait, true).Select(s => (s.Width, s.Height)));
        }

        [Fact]
        public void A_non_open_image_still_gets_only_small_thumbnails()
        {
            ImageApiSizeHelper.GetPossibleSizes(Portrait, false, 1000)
                .Should().OnlyContain(s => s.Width <= 200);
        }
    }
}
