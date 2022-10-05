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
        private const string MsForm = "MS.126";
        private const string ArchiveFormWithSlashes = "PPCRI/D/4/5A";
        private const string ArchiveFormNoSlashes = "PPCRI_D_4_5A";
        private const string NotBNumberButHasParts = "2b99977766_0002_0005";
        private const string MixedSlashesAndUnderscores = "PPCRI_2/b12312345/_a";

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
            identifier.PackageIdentifier.Should().Be("b99977766");
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
            identifier.PackageIdentifier.Should().Be("b99977766");
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
            identifier.PackageIdentifier.Should().Be("b99977766");
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
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0002");
            identifier.IssuePart.Should().Be("b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void Ctor_Correct_NotBNumber()
        {
            // Act
            var identifier = new DdsIdentifier(NotBNumberButHasParts);
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.NonBNumber);

            // This might seem wrong. But with just CALM IDs and bNumbers (or starts with b numbers),
            // we can have a rule that any non-bnumber identifier with slashes can have those slashes
            // converted to underscores. https://github.com/wellcomecollection/platform/issues/5498#issuecomment-1218213009
            
            var expectedPackageIdentifier = NotBNumberButHasParts.Replace('_', '/');
            // The whole identifier
            identifier.PackageIdentifier.Should().Be(expectedPackageIdentifier); 
            identifier.VolumePart.Should().BeNull("It doesn't start with a B number");
            identifier.IssuePart.Should().BeNull("It doesn't start with a B number");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_BNumber()
        {
            // Act
            DdsIdentifier identifier = BNumber;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.BNumber);
            identifier.PackageIdentifier.Should().Be("b99977766");
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
            identifier.PackageIdentifier.Should().Be("b99977766");
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
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_123"); 
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
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0002");
            identifier.IssuePart.Should().Be("b99977766_0002_0005");
            identifier.SequenceIndex.Should().Be(0);
        }
        
        [Fact]
        public void ImplicitToDdsIdentifier_Correct_Unknown()
        {
            // Act
            DdsIdentifier identifier = NotBNumberButHasParts;
            
            // Assert
            identifier.IdentifierType.Should().Be(IdentifierType.NonBNumber);
            
            var expectedPackageIdentifier = NotBNumberButHasParts.Replace('_', '/');
            identifier.PackageIdentifier.Should().Be(expectedPackageIdentifier); 
            identifier.VolumePart.Should().BeNull("It doesn't start with a B number");
            identifier.IssuePart.Should().BeNull("It doesn't start with a B number");
            identifier.SequenceIndex.Should().Be(0);
        }

        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        [InlineData(MsForm)]
        [InlineData(ArchiveFormWithSlashes)]
        public void ToString_ReturnsOriginalValue_ForSafeForms(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.ToString().Should().Be(value);
        }
        
        
        [Theory]
        [InlineData(NotBNumberButHasParts, "2b99977766/0002/0005")]
        [InlineData(ArchiveFormNoSlashes, "PPCRI/D/4/5A")]
        [InlineData(MixedSlashesAndUnderscores, "PPCRI/2/b12312345//a")] // yes really
        public void ToString_ReturnsCanonicalValue_ForUnSafeForms(string value, string expected)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.ToString().Should().Be(expected);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        [InlineData(MsForm)]
        [InlineData(ArchiveFormWithSlashes)]
        public void ImplicitToString_ReturnsOriginalValue_ForSafeForms(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Act
            string strValue = identifier;
            
            // Assert
            strValue.Should().Be(value);
        }
        
        [Theory]
        [InlineData(NotBNumberButHasParts, "2b99977766/0002/0005")]
        [InlineData(ArchiveFormNoSlashes, "PPCRI/D/4/5A")]
        [InlineData(MixedSlashesAndUnderscores, "PPCRI/2/b12312345//a")] // yes really
        public void ImplicitToString_ReturnsCanonicalValue_ForUnsafeForms(string value, string expected)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Act
            string strValue = identifier;
            
            // Assert
            strValue.Should().Be(expected);
        }

        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.HasBNumber.Should().BeTrue();
            identifier.BNumber.Should().Be(BNumber);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PackageIdentfier(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(BNumber);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PackageIdentifierPathElementSafe(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.PackageIdentifierPathElementSafe.Should().Be(BNumber);
        }
        
                
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PathElementSafe(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.PathElementSafe.Should().Be(value);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(BNumberSequence)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_Digitised_Storage_Type(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.StorageSpace.Should().Be("digitised");
        }
        
        [Theory]
        [InlineData(MsForm, MsForm)]
        [InlineData(ArchiveFormWithSlashes, ArchiveFormNoSlashes)]
        [InlineData(ArchiveFormNoSlashes, ArchiveFormNoSlashes)]
        [InlineData(NotBNumberButHasParts, NotBNumberButHasParts)]
        [InlineData(MixedSlashesAndUnderscores, "PPCRI_2_b12312345__a")]
        public void Non_BNumbers_Yields_Same_PathElementSafe(string value, string expected)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.PackageIdentifierPathElementSafe.Should().Be(expected);
            identifier.PathElementSafe.Should().Be(expected);
        }

        [Theory]
        [InlineData(MsForm)]
        [InlineData(ArchiveFormWithSlashes)]
        [InlineData(ArchiveFormNoSlashes)]
        [InlineData(NotBNumberButHasParts)]
        [InlineData(MixedSlashesAndUnderscores)]
        public void Non_BNumbers_Dont_Have_BNumbers(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.HasBNumber.Should().BeFalse();
            identifier.BNumber.Should().BeNull();
        }
        
        [Theory]
        [InlineData(MsForm)]
        [InlineData(ArchiveFormWithSlashes)]
        [InlineData(ArchiveFormNoSlashes)]
        [InlineData(NotBNumberButHasParts)]
        [InlineData(MixedSlashesAndUnderscores)]
        public void If_NonBNumber_Then_Storage_is_BornDigital(string value)
        {
            // Arrange
            var identifier = new DdsIdentifier(value);
            
            // Assert
            identifier.StorageSpace.Should().Be("born-digital");
        }

        [Fact]
        public void No_Slashes_Or_Underscores_Yields_Same_Id()
        {
            // Arrange
            var identifier = new DdsIdentifier(MsForm);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(MsForm);
            identifier.PackageIdentifierPathElementSafe.Should().Be(MsForm);
        }

        [Fact]
        public void Package_Identifier_Has_Slashes()
        {
            // Arrange
            var identifier = new DdsIdentifier(ArchiveFormNoSlashes);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
        }
        
        
        [Fact]
        public void Package_Identifier_Preserves_Slashes()
        {
            // Arrange
            var identifier = new DdsIdentifier(ArchiveFormWithSlashes);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
        }
        
        
        [Fact]
        public void Path_Element_Safe_Form_Round_Trips()
        {
            // Arrange
            var identifier = new DdsIdentifier(ArchiveFormNoSlashes);
            
            // Act
            var packageIdentifier = identifier.PackageIdentifier;
            var identifier2 = new DdsIdentifier(packageIdentifier);
            
            // Assert
            identifier2.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
            identifier2.PackageIdentifierPathElementSafe.Should().Be(ArchiveFormNoSlashes);
        }
    }
}