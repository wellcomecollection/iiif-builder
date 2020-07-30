using System;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.Common.Tests
{
    public class DdsIdentifierTests
    {
        private const string BNumber = "b99977766";
        private const string Volume = "b99977766_0003";
        private const string Issue = "b99977766_0002_0005";
        private const string BNumberSequence = "b99977766/123";
        private const string Unknown = "2b99977766_0002_0005/123";

        [Theory(Skip = "Verify this is expected behaviour")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Ctor_Throws_IfNullOrWhitespaceValue(string value)
        {
            // Act
            Action action = () => new DdsIdentifier(value);
            
            // Assert
            action.Should().Throw<ArgumentNullException>();
        }
        
        [Fact]
        public void Ctor_Correct_BNumber()
        {
            // Act
            var identifier = new DdsIdentifier(BNumber);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.BNumber);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().BeNull();
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void Ctor_Correct_Volume()
        {
            // Act
            var identifier = new DdsIdentifier(Volume);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Volume);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0003");
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void Ctor_Correct_BNumberAndSequenceIndex()
        {
            // Act
            var identifier = new DdsIdentifier(BNumberSequence);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.BNumberAndSequenceIndex);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_123"); // NOTE: This doesn't seem right?
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(123);
        }
        
        [Fact]
        public void Ctor_Correct_Issue()
        {
            // Act
            var identifier = new DdsIdentifier(Issue);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Issue);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0002");
            identifier.IssuePart.Should().Be("b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void Ctor_Correct_Unknown()
        {
            // Act
            var identifier = new DdsIdentifier(Unknown);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Unknown);
            
            // NOTE: These don't seem right
            identifier.BNumber.Should().Be("2b99977766"); 
            identifier.VolumePart.Should().Be("2b99977766_0002");
            identifier.IssuePart.Should().Be("2b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_BNumber()
        {
            // Act
            DdsIdentifier identifier = BNumber;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.BNumber);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().BeNull();
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_Volume()
        {
            // Act
            DdsIdentifier identifier = Volume;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Volume);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0003");
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_BNumberAndSequenceIndex()
        {
            // Act
            DdsIdentifier identifier = BNumberSequence;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.BNumberAndSequenceIndex);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_123"); //NOTE: This doesn't seem right
            identifier.IssuePart.Should().BeNull();
            identifier.SequenceIndex.Should().Be(123);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_Issue()
        {
            // Act
            DdsIdentifier identifier = Issue;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Issue);
            identifier.BNumber.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0002");
            identifier.IssuePart.Should().Be("b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_Unknown()
        {
            // Act
            DdsIdentifier identifier = Unknown;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.Unknown);
            
            // NOTE: These don't seem right
            identifier.BNumber.Should().Be("2b99977766"); 
            identifier.VolumePart.Should().Be("2b99977766_0002");
            identifier.IssuePart.Should().Be("2b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }

        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        [InlineData(Unknown)]
        public void ToString_ReturnsOriginalValue(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.ToString().Should().Be(value);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        [InlineData(Unknown)]
        public void ImplicitToString_ReturnsOriginalValue(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Act
            string strValue = identifier;
            
            // Assert
            strValue.Should().Be(value);
        }
    }
}