using FluentAssertions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Xunit;

namespace Wellcome.Dds.Tests.IIIFBuilding
{
    public class UriPatternsTests
    {
        private readonly UriPatterns sut;

        public UriPatternsTests()
        {
            var ddsOptions = new DdsOptions
            {
                LinkedDataDomain = "https://test.linkeddata",
                WellcomeCollectionApi = "https://test.wellcomeapi",
                ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
            };

            sut = new UriPatterns(Options.Create(ddsOptions));
        }
        
        [Fact]
        public void GetCacheInvalidationPaths_IIIF_Correct()
        {
            // Arrange
            const string identifier = "b1231231";
            var expected = new[]
            {
                "/thumb/b1231231*",
                "/search/v0/b1231231*",
                "/search/v1/b1231231*",
                "/search/v2/b1231231*",
                "/search/autocomplete/v1/b1231231*",
                "/search/autocomplete/v2/b1231231*",
                "/annotations/v2/b1231231*",
                "/annotations/v3/b1231231*",
                "/presentation/b1231231*",
                "/presentation/v2/b1231231*",
                "/presentation/v3/b1231231*",
            };

            // Act
            var actual = sut.GetCacheInvalidationPaths(identifier, InvalidationPathType.IIIF);

            // Assert
            actual.Should().BeEquivalentTo(expected);
        }
        
        [Fact]
        public void GetCacheInvalidationPaths_Text_Correct()
        {
            // Arrange
            const string identifier = "b1231231";
            var expected = new[]
            {
                "/text/v1/b1231231*",
                "/text/alto/b1231231*",
            };

            // Act
            var actual = sut.GetCacheInvalidationPaths(identifier, InvalidationPathType.Text);

            // Assert
            actual.Should().BeEquivalentTo(expected);
        }
    }
}