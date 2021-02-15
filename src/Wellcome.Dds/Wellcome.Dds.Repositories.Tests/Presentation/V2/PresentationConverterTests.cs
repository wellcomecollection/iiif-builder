using System;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Constants;
using Wellcome.Dds.Repositories.Presentation.V2;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Presentation.V2
{
    public class PresentationConverterTests
    {
        private readonly PresentationConverter sut;

        public PresentationConverterTests()
        {
            sut = new PresentationConverter();
        }
        
        [Fact]
        public void Convert_Throws_IfPassedNull()
        {
            // Arrange
            Action action = () => sut.Convert(null!);

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }
        
        [Fact]
        public void Convert_Throws_IfNonManifestOrCollection()
        {
            // Arrange
            Action action = () => sut.Convert(new Canvas());

            // Assert
            action.Should()
                .Throw<ArgumentException>()
                .WithMessage("Unable to convert IIIF.Presentation.V3.Canvas to v2. Expected: Canvas or Manifest (Parameter 'presentation')");
        }

        [Fact]
        public void Convert_FromManifest_Minimum()
        {
            // Arrange
            var manifest = new Manifest {Id = "/presentation/b12312312"};
            manifest.EnsureContext(IIIF.Presentation.Context.V3);

            // Act
            var result = sut.Convert(manifest);
            
            // Assert
            result.Context.Should().BeOfType<string>().Which.Should().Be(IIIF.Presentation.Context.V2);
        }
        
        [Fact]
        public void Convert_FromCollection_Minimum()
        {
            // Arrange
            var collection = new Collection {Id = "/presentation/b12312312"};
            collection.EnsureContext(IIIF.Presentation.Context.V3);

            // Act
            var result = sut.Convert(collection);
            
            // Assert
            result.Context.Should().BeOfType<string>().Which.Should().Be(IIIF.Presentation.Context.V2);
        }
    }
}