using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Wellcome.Dds.Server.Conneg;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Conneg
{
    public class IIIFPresentationTests
    {
        [Fact]
        public void GetIIIFPresentationType_Unknown_IfMediaTypeHeadersNull()
        {
            // Arrange
            IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders = null;
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType();

            // Assert
            result.Should().Be(IIIFPresentationVersion.Unknown);
        }
        
        [Fact]
        public void GetIIIFPresentationType_Unknown_IfMediaTypeHeadersEmpty()
        {
            // Arrange
            var mediaTypeHeaders = new MediaTypeHeaderValue[0];
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType();

            // Assert
            result.Should().Be(IIIFPresentationVersion.Unknown);
        }
        
        [Fact]
        public void GetIIIFPresentationType_Unknown_IfNoKnownIIIFPresentationType()
        {
            // Arrange
            var mediaTypeHeaders = new[] {MediaTypeHeaderValue.Parse("application/json")};
            
            // Act
            var result = mediaTypeHeaders.GetIIIFPresentationType();

            // Assert
            result.Should().Be(IIIFPresentationVersion.Unknown);
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