using System;
using FluentAssertions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation.V2;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Presentation.V2
{
    public class PresentationConverterTests
    {
        private readonly PresentationConverter sut;

        public PresentationConverterTests()
        {
            sut = new PresentationConverter(null, NullLogger.Instance);
        }
        
        [Fact]
        public void Convert_Throws_IfPassedNull()
        {
            // Arrange
            Action action = () => sut.Convert(null!, "b10727000");

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }
        
        [Fact]
        public void Convert_Throws_IfNonManifestOrCollection()
        {
            // Arrange
            Action action = () => sut.Convert(new Canvas(), "b10727000");

            // Assert
            action.Should().Throw<IIIFBuildStateException>();
        }

        [Fact]
        public void Convert_FromManifest_Minimum()
        {
            // Arrange
            var manifest = new Manifest {Id = "/presentation/b12312312"};
            manifest.EnsureContext(IIIF.Presentation.Context.V3);

            // Act
            var result = sut.Convert(manifest, "b10727000");
            
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
            var result = sut.Convert(collection, "b10727000");
            
            // Assert
            result.Context.Should().BeOfType<string>().Which.Should().Be(IIIF.Presentation.Context.V2);
        }
    }
}