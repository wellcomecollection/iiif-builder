using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Wellcome.Dds.Server.Conneg;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Conneg
{
    public class IIIFPresentationTests
    {
        [Theory]
        [InlineData(IIIFPresentationVersion.Unknown)]
        [InlineData(IIIFPresentationVersion.V2)]
        [InlineData(IIIFPresentationVersion.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfMediaTypeHeadersNull(IIIFPresentationVersion fallback)
        {
            // Arrange
            IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders = null;
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData(IIIFPresentationVersion.Unknown)]
        [InlineData(IIIFPresentationVersion.V2)]
        [InlineData(IIIFPresentationVersion.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfMediaTypeHeadersEmpty(IIIFPresentationVersion fallback)
        {
            // Arrange
            var mediaTypeHeaders = new MediaTypeHeaderValue[0];
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData(IIIFPresentationVersion.Unknown)]
        [InlineData(IIIFPresentationVersion.V2)]
        [InlineData(IIIFPresentationVersion.V3)]
        public void GetIIIFPresentationType_FallbackVersion_IfNoKnownIIIFPresentationType(IIIFPresentationVersion fallback)
        {
            // Arrange
            var mediaTypeHeaders = new[] {MediaTypeHeaderValue.Parse("application/json")};
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType(fallback);

            // Assert
            result.Should().Be(fallback);
        }
        
        [Theory]
        [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"", IIIFPresentationVersion.V2)]
        [InlineData("application/json;profile=\"http://iiif.io/api/presentation/3/context.json\"", IIIFPresentationVersion.V3)]
        public void GetIIIFPresentationType_ReturnsExpectedType(string mediaType, IIIFPresentationVersion expected)
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
            result.Should().Be(IIIFPresentationVersion.V3);
        }
    }
}