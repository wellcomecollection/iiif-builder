using System;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        private IIdentityService identityService;
        
        public DdsIdentifierTests()
        {
            identityService = new ParsingIdentityService(new NullLogger<ParsingIdentityService>(), new MemoryCache(new MemoryCacheOptions()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GetIdentity_Throws_IfNullOrWhitespaceValue(string value)
        {
            // Act
            Action action = () => identityService.GetIdentity(value);
            
            // Assert
            action.Should().Throw<FormatException>();
        }
        
        [Fact]
        public void Ctor_Correct_BNumber()
        {
            // Act
            var identifier = identityService.GetIdentity(BNumber);
            
            // Assert
            identifier.Generator.Should().Be(Generator.Goobi);
            identifier.Source.Should().Be(Source.Sierra);
            identifier.StorageSpace.Should().Be(StorageSpace.Digitised);
            identifier.IsPackageLevelIdentifier.Should().Be(true);
            identifier.PackageIdentifier.Should().Be(BNumber);
            identifier.PackageIdentifierPathElementSafe.Should().Be(BNumber);
            identifier.PathElementSafe.Should().Be(BNumber);
            identifier.Value.Should().Be(BNumber);
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().BeNull();
            identifier.IssuePart.Should().BeNull();
        }
        
        [Fact]
        public void Ctor_Correct_Volume()
        {
            // Act
            var identifier = identityService.GetIdentity(Volume);
            
            // Assert
            identifier.Generator.Should().Be(Generator.Goobi);
            identifier.Source.Should().Be(Source.Sierra);
            identifier.StorageSpace.Should().Be(StorageSpace.Digitised);
            identifier.IsPackageLevelIdentifier.Should().Be(false);
            identifier.PackageIdentifier.Should().Be(BNumber);
            identifier.PackageIdentifierPathElementSafe.Should().Be(BNumber);
            identifier.PathElementSafe.Should().Be(Volume);
            identifier.Value.Should().Be(Volume);
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0003");
            identifier.IssuePart.Should().BeNull();
        }
        
        [Fact]
        public void Ctor_Correct_BNumberAndSequenceIndex()
        {
            // Act
            Action action = () => identityService.GetIdentity(BNumberSequence);
            
            // Assert
            action.Should().Throw<FormatException>();
            // "b99977766/123" format no longer supported
        }
        
        [Fact]
        public void Ctor_Correct_Issue()
        {
            // Act
            var identifier = identityService.GetIdentity(Issue);
            
            // Assert
            BNumber.Should().Be("b99977766");
            identifier.Generator.Should().Be(Generator.Goobi);
            identifier.Source.Should().Be(Source.Sierra);
            identifier.StorageSpace.Should().Be(StorageSpace.Digitised);
            identifier.IsPackageLevelIdentifier.Should().Be(false);
            identifier.PackageIdentifier.Should().Be(BNumber);
            identifier.PackageIdentifierPathElementSafe.Should().Be(BNumber);
            identifier.PathElementSafe.Should().Be(Issue);
            identifier.Value.Should().Be(Issue);
            identifier.PackageIdentifier.Should().Be("b99977766");
            identifier.VolumePart.Should().Be("b99977766_0002");
            identifier.IssuePart.Should().Be("b99977766_0002_0005");
        }
        
        [Fact]
        public void Ctor_Correct_NotBNumber()
        {
            // Act
            var identifier = identityService.GetIdentity(NotBNumberButHasParts);
            
            // Assert
            identifier.Generator.Should().Be(Generator.Archivematica);
            identifier.Source.Should().Be(Source.Calm);
            identifier.StorageSpace.Should().Be(StorageSpace.BornDigital);
            identifier.IsPackageLevelIdentifier.Should().Be(true);
            
            var expectedPackageIdentifier = NotBNumberButHasParts.Replace('_', '/');
            identifier.PackageIdentifier.Should().Be(expectedPackageIdentifier);
            identifier.PackageIdentifierPathElementSafe.Should().Be(NotBNumberButHasParts);
            identifier.PathElementSafe.Should().Be(NotBNumberButHasParts);
            identifier.Value.Should().Be(expectedPackageIdentifier);
            identifier.VolumePart.Should().BeNull("It doesn't start with a B number");
            identifier.IssuePart.Should().BeNull("It doesn't start with a B number");

            // This might seem wrong. But with just CALM IDs and bNumbers (or starts with b numbers),
            // we can have a rule that any non-bnumber identifier with slashes can have those slashes
            // converted to underscores. https://github.com/wellcomecollection/platform/issues/5498#issuecomment-1218213009
        }

        

        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(Issue)]
        [InlineData(MsForm)]
        [InlineData(ArchiveFormWithSlashes)]
        public void ToString_ReturnsOriginalValue_ForSafeForms(string value)
        {
            // Arrange
            var identifier = identityService.GetIdentity(value);
            
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
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.ToString().Should().Be(expected);
        }
        

        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber(string value)
        {
            // Arrange
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.Generator.Should().Be(Generator.Goobi);
            identifier.Source.Should().Be(Source.Sierra);
            identifier.StorageSpace.Should().Be(StorageSpace.Digitised);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PackageIdentifier(string value)
        {
            // Arrange
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(BNumber);
        }
        
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PackageIdentifierPathElementSafe(string value)
        {
            // Arrange
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.PackageIdentifierPathElementSafe.Should().Be(BNumber);
        }
        
                
        [Theory]
        [InlineData(BNumber)]
        [InlineData(Volume)]
        [InlineData(Issue)]
        public void BNumber_Forms_Yield_BNumber_PathElementSafe(string value)
        {
            // Arrange
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.PathElementSafe.Should().Be(value);
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
            var identifier = identityService.GetIdentity(value);
            
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
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.Generator.Should().Be(Generator.Archivematica);
            identifier.Source.Should().Be(Source.Calm);
            identifier.StorageSpace.Should().Be(StorageSpace.BornDigital);
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
            var identifier = identityService.GetIdentity(value);
            
            // Assert
            identifier.StorageSpace.Should().Be(StorageSpace.BornDigital);
        }

        [Fact]
        public void No_Slashes_Or_Underscores_Yields_Same_Id()
        {
            // Arrange
            var identifier = identityService.GetIdentity(MsForm);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(MsForm);
            identifier.PackageIdentifierPathElementSafe.Should().Be(MsForm);
        }

        [Fact]
        public void Package_Identifier_Has_Slashes()
        {
            // Arrange
            var identifier = identityService.GetIdentity(ArchiveFormNoSlashes);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
        }
        
        
        [Fact]
        public void Package_Identifier_Preserves_Slashes()
        {
            // Arrange
            var identifier = identityService.GetIdentity(ArchiveFormWithSlashes);
            
            // Assert
            identifier.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
        }
        
        
        [Fact]
        public void Path_Element_Safe_Form_Round_Trips()
        {
            // Arrange
            var identifier = identityService.GetIdentity(ArchiveFormNoSlashes);
            
            // Act
            var packageIdentifier = identifier.PackageIdentifier;
            var identifier2 = identityService.GetIdentity(packageIdentifier);
            
            // Assert
            identifier2.PackageIdentifier.Should().Be(ArchiveFormWithSlashes);
            identifier2.PackageIdentifierPathElementSafe.Should().Be(ArchiveFormNoSlashes);
        }
    }
}