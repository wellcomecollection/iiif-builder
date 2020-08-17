using System;
using FluentAssertions;
using Utils.Guard;
using Xunit;

namespace Utils.Tests
{
    public class GuardXTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ThrowIfNullOrEmpty_Throws_IfNullOrEmpty(string val)
        {
            // Act
            Action action = () => GuardX.ThrowIfNullOrEmpty(val, "foo");

            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'foo')");
        }

        [Fact]
        public void ThrowIfNullOrEmpty_ReturnsProvidedString_IfNotNullOrEmpty()
        {
            // Arrange
            const string val = "foo bar";
            
            // Act
            var actual = val.ThrowIfNullOrEmpty(nameof(val));
            
            // Assert
            actual.Should().Be(val);
        }
        
        [Fact]
        public void ThrowIfNull_Throws_IfNull()
        {
            // Act
            object o = null;
            Action action = () => o.ThrowIfNull("foo");

            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'foo')");
        }

        [Fact]
        public void ThrowIfNull_ReturnsObject_IfNotNull()
        {
            // Arrange
            var val = DateTime.Today;
            
            // Act
            var actual = val.ThrowIfNull(nameof(val));
            
            // Assert
            actual.Should().Be(val);
        }
    }
}