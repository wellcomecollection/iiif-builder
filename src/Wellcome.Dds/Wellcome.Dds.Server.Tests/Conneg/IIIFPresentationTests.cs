using System.Collections.Generic;
using FluentAssertions;
using IIIF.Presentation;
using Microsoft.Net.Http.Headers;
using Wellcome.Dds.Server.Conneg;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Conneg
{
    public class IIIFPresentationTests
    {
        [Theory]
        [InlineData(Version.Unknown)]
        [InlineData(Version.V2)]
        [InlineData(Version.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfMediaTypeHeadersNull(Version fallback)
        {
            // Arrange
            IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders = null;
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData(Version.Unknown)]
        [InlineData(Version.V2)]
        [InlineData(Version.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfMediaTypeHeadersEmpty(Version fallback)
        {
            // Arrange
            var mediaTypeHeaders = new MediaTypeHeaderValue[0];
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData(Version.Unknown)]
        [InlineData(Version.V2)]
        [InlineData(Version.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfNoKnownIIIFPresentationType(Version fallback)
        {
            // Arrange
            var mediaTypeHeaders = new[] {MediaTypeHeaderValue.Parse("application/json")};
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"", Version.V2)]
        [InlineData("application/json;profile=\"http://iiif.io/api/presentation/3/context.json\"", Version.V3)]
        public void GetIIIFPresentationType_ReturnsExpectedType(string mediaType, Version expected)
        {
            // Arrange
            var mediaTypeHeaders = new[] {MediaTypeHeaderValue.Parse(mediaType)};
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType();

            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void GetIIIFPresentationType_PrefersLatestVersion()
        {
            // Arrange
            const string v2 = "application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"";
            const string v3 = "application/json;profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
            var mediaTypeHeaders = new[] {MediaTypeHeaderValue.Parse(v2), MediaTypeHeaderValue.Parse(v3)};
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType();

            // Assert
            result.Should().Be(Version.V3);
        }
    }
}