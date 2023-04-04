using FluentAssertions;
using Xunit;

namespace Utils.Tests
{
    public class StringUtilsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void HasText_False_IfNullOrWhitespace(string str)
            => str.HasText().Should().BeFalse();
        
        [Fact]
        public void HasText_True_IfNotNullOrWhitespace()
            => "foo".HasText().Should().BeTrue();
        
        [Fact]
        public void RemoveStart_ReturnsNull_IfStringNull()
        {
            // Arrange 
            const string str = null;
            
            // Act
            var result = str.RemoveStart("hi");
            
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public void RemoveStart_ReturnsEmptyString_IfStringEmpty()
        {
            // Arrange 
            const string str = "";
            
            // Act
            var result = str.RemoveStart("hi");
            
            // Assert
            result.Should().BeEmpty();
        }
        
        [Fact]
        public void RemoveStart_RemovesStart_IfProvidedStringStatsWith()
        {
            // Arrange 
            const string str = "something";
            const string expected = "thing";
            
            // Act
            var result = str.RemoveStart("some");
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void RemoveStart_ReturnsOriginalString_IfDoesntStartWith()
        {
            // Arrange 
            const string str = "something";

            // Act
            var result = str.RemoveStart("omet");
            
            // Assert
            result.Should().Be(str);
        }
        
        [Fact]
        public void RemoveStart_ReturnsOriginalString_IfStartsWithIsFullString()
        {
            // Arrange 
            const string str = "something";

            // Act
            var result = str.RemoveStart("something");
            
            // Assert
            result.Should().Be(str);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void SummariseWithEllipsis_HandlesNullOrWhitespace(string str)
            => str.SummariseWithEllipsis(10).Should().Be(str);

        [Fact]
        public void SummariseWithEllipsis_ReturnsExpected()
        {
            // Arrange
            const string str = "I do not like <b>green</b> eggs and ham";
            const string expected = "I do not like green eggs...";
            
            // Act
            var result = str.SummariseWithEllipsis(27);
            
            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Chomp_ReturnsProvided_IfNullOrEmpty(string toChomp)
        {
            // Act
            var actual = toChomp.Chomp("foo");
            
            // Assert
            actual.Should().Be(toChomp);
        }

        [Fact]
        public void Chomp_ReturnsProvidedString_IfDoesntEndWithSeparator()
        {
            // Arrange
            const string toChomp = "something";
            const string separator = "some";
            const string expected = "something";

            // Act
            var actual = toChomp.Chomp(separator);

            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void Chomp_RemovesEndOfString()
        {
            // Arrange
            const string toChomp = "something";
            const string separator = "thing";
            const string expected = "some";

            // Act
            var actual = toChomp.Chomp(separator);

            // Assert
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("path", "path")]
        [InlineData("/path", "path")]
        [InlineData("https://example.org/some/path", "path")]
        [InlineData("https://example.org/some/long/path", "https://example.org/some/long/path")]
        [InlineData("https://example.org/some/xxx/path", "https://example.org/some/long/path")]
        [InlineData("https://example.org/some/long/path", "https://example.org/some/long/path", 2)]
        [InlineData("https://example.org/xxx/long/path", "https://example.org/some/long/path", 2)]
        public void PathElements_Are_Equivalent(string path1, string path2, int walkback = 1)
        {
            StringUtils.EndWithSamePathElements(path1, path2, walkback).Should().BeTrue();
        }
        
        [Theory]
        [InlineData(null, "xxx")]
        [InlineData("", "xxx")]
        [InlineData("path", "xxx")]
        [InlineData("/path", "xxx")]
        [InlineData("https://example.org/some/path", "xxx")]
        [InlineData("https://example.org/some/long/path", "https://example.org/some/long/xxx")]
        [InlineData("https://example.org/some/long/path", "https://example.org/some/xxx/path", 2)]
        [InlineData("https://example.org/xxx/long/path", "https://example.org/some/long/path", 3)]
        public void PathElements_Are_Not_Equivalent(string path1, string path2, int walkback = 1)
        {
            StringUtils.EndWithSamePathElements(path1, path2, walkback).Should().BeFalse();
        }
    }
}
