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
    }
}
