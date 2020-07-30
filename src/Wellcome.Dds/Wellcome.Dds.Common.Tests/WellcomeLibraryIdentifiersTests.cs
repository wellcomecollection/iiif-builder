using System;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.Common.Tests
{
    public class WellcomeLibraryIdentifiersTests
    {
        [Theory]
        [InlineData("b12345672")]
        [InlineData(".b12345672")]
        [InlineData("B12345672")]
        [InlineData(".B12345672")]
        [InlineData(".b123456721231")]
        [InlineData("b123456721231")]
        [InlineData("B123456721231")]
        public void GetNormalisedBNumber_ReturnsNormalisedbNumber_IfValid(string bNumber)
        {
            // Arrange
            const string normalised = "b12345672";
            
            // Act
            var result = WellcomeLibraryIdentifiers.GetNormalisedBNumber(bNumber, true);
            
            // Assert
            result.Should().Be(normalised);
        }

        [Fact]
        public void GetNormalisedBNumber_ThrowsOnInvalidChecksum_IfSpecified()
        {
            // Arrange
            const string bNumber = "b12345671";
            
            // Act
            Action action = () => WellcomeLibraryIdentifiers.GetNormalisedBNumber(bNumber, true);
            
            // Assert
            action.Should().Throw<ArgumentException>();
        }
        
        [Fact]
        public void GetNormalisedBNumber_ReturnsNull_IfNotValid()
        {
            // Act
            var result = WellcomeLibraryIdentifiers.GetNormalisedBNumber("foo", true);
            
            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("123456")]
        [InlineData("12345678")]
        public void GetExpectedBNumberCheckDigit_Throws_IfWrongLength(string number)
        {
            Action action = () => WellcomeLibraryIdentifiers.GetExpectedBNumberCheckDigit(number);
            action.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("2247126", '1')]
        [InlineData("1833064", '2')]
        [InlineData("1939460", '3')]
        [InlineData("1949883", '4')]
        [InlineData("1923597", '5')]
        [InlineData("1944099", '6')]
        [InlineData("1885421", '7')]
        [InlineData("1947411", '8')]
        [InlineData("2010965", '9')]
        [InlineData("1847424", 'x')]
        public void GetExpectedBNumberCheckDigit_Correct(string number, char checkDigit) 
            => WellcomeLibraryIdentifiers.GetExpectedBNumberCheckDigit(number).Should().Be(checkDigit);
    }
}