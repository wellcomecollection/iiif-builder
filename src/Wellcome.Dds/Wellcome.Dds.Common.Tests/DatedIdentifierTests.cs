using System;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.Common.Tests
{
    public class DatedIdentifierTests
    {
        [Fact]
        public void ToString_EmptyObject_ReturnsExpected()
        {
            // Arrange
            var identifier = new DatedIdentifier();
            const string expected = "[DatedIdentifier]  0001-01-01T00:00:00 ";

            // Act
            var actual = identifier.ToString();
            
            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void ToString_ReturnsExpected()
        {
            // Arrange
            var identifier = new DatedIdentifier
            {
                Identifier = "foo", Label = "bar", Date = new DateTime(2020, 10, 14, 22, 10, 5)
            };
            const string expected = "[DatedIdentifier] foo 2020-10-14T22:10:05 bar";

            // Act
            var actual = identifier.ToString();
            
            // Assert
            actual.Should().Be(expected);
        }
    }
}