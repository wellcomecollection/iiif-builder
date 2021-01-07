using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.AssetDomainRepositories.Storage.FileSystem;
using Xunit;
using MetsManifestation = Wellcome.Dds.AssetDomainRepositories.Mets.Model.Manifestation;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class MetsRepositoryTests
    {
        private readonly IWorkStorageFactory workStorageFactory;
        
        public MetsRepositoryTests()
        {
            var fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileSystemMetsFixtures");
            workStorageFactory = new FileSystemWorkStorageFactory(fixtures);
        }

        [Fact]
        public async Task Old_Workflow_Video_Yields_Collection()
        {
            var metsRepository = new MetsRepository(workStorageFactory);
            var b16675630 = await metsRepository.GetAsync("b16675630");
            b16675630.Should().BeOfType<Collection>();
            b16675630.Partial.Should().BeFalse();
            var m0 = ((Collection) b16675630).Manifestations[0];
            var b16675630_0001 = await metsRepository.GetAsync(m0.Id);
            b16675630_0001.Should().BeOfType<MetsManifestation>();
            b16675630_0001.Type.Should().Be("Video");
            var mb16675630_0001 = (MetsManifestation) b16675630_0001;
            mb16675630_0001.Partial.Should().BeFalse();
            mb16675630_0001.PosterImage.RelativePath.Should().Be("posters/0055-0000-3718-0000-0-0000-0000-0.jpg");
            var physFile = mb16675630_0001.Sequence[0];
            physFile.Type.Should().Be("page"); // yes.. in METS...
            physFile.RelativePath.Should().Be("objects/0055-0000-3718-0000-0-0000-0000-0.mpg");
            physFile.RelativePosterPath.Should().BeNull();
            physFile.RelativeTranscriptPath.Should().BeNull();
            physFile.RelativeMasterPath.Should().BeNull();
            physFile.RelativeAltoPath.Should().BeNull();
            physFile.AssetMetadata.Should().BeOfType<PremisMetadata>();
            // This property should reflect the access file metadata, not any of the others
            physFile.AssetMetadata.GetFileName().Should().Be("0055-0000-3718-0000-0-0000-0000-0.mpg");
            physFile.AssetMetadata.GetFormatName().Should().Be("MPEG-2 Video Format");
            physFile.AssetMetadata.GetLengthInSeconds().Should().Be("14mn 20s");
            
            // old poster image
            mb16675630_0001.PosterImage.Should().BeOfType<StoredFile>();
            mb16675630_0001.PosterImage.RelativePath.Should().Be("posters/0055-0000-3718-0000-0-0000-0000-0.jpg");

        }

        [Fact]
        public async Task New_Workflow_Video_Yields_Multiple_Files()
        {
            var metsRepository = new MetsRepository(workStorageFactory);
            var b30496160 = await metsRepository.GetAsync("b30496160");
            b30496160.Should().BeOfType<MetsManifestation>();
            b30496160.Partial.Should().BeFalse();
            var mb30496160 = (MetsManifestation) b30496160;
            mb30496160.Type.Should().Be("Video");
            mb30496160.Label.Should().Be("Anaesthesia in the horse, cow, pig, dog and cat.");
            mb30496160.SignificantSequence.Count.Should().Be(1);
            mb30496160.Sequence.Count.Should().Be(1);
            var physFile = mb30496160.Sequence[0];
            physFile.Type.Should().Be("MP4"); // unexpected...
            physFile.RelativePath.Should().Be("objects/b30496160_0002.mp4");
            physFile.RelativePosterPath.Should().Be("objects/b30496160_0001.jpg");
            physFile.RelativeTranscriptPath.Should().Be("objects/b30496160_0004.pdf");
            physFile.RelativeMasterPath.Should().Be("objects/b30496160_0003.mxf");
            physFile.RelativeAltoPath.Should().BeNull();
            physFile.AssetMetadata.Should().BeOfType<PremisMetadata>();
            // This property should reflect the access file metadata, not any of the others
            physFile.AssetMetadata.GetFileName().Should().Be("b30496160_0002.mp4");
            physFile.AssetMetadata.GetFormatName().Should().Be("MPEG-4 Media File");
            physFile.AssetMetadata.GetLengthInSeconds().Should().Be("58s");
            
            // we retain physFile.AssetMetadata as the access file's metadata.
            // But we allow access to the assetMetadata of the other files through a new lookup:
            physFile.Files.Count.Should().Be(4);
            
            
            // new poster image
            mb30496160.PosterImage.Should().BeOfType<StoredFile>();
            mb30496160.PosterImage.RelativePath.Should().Be("objects/b30496160_0001.jpg");
            
            // Transcript with metadata
            physFile.Files.Should().Contain(f => f.Use == "TRANSCRIPT");
            var transcript = physFile.Files.Single(f => f.Use == "TRANSCRIPT");
            transcript.Family.Should().Be(AssetFamily.File);
            transcript.RelativePath.Should().Be(physFile.RelativeTranscriptPath);
            transcript.AssetMetadata.GetNumberOfPages().Should().Be(86);



        }

        public async Task New_Model_Supports_Old_Alto()
        {
            var metsRepository = new MetsRepository(workStorageFactory);
            var b30074976 = await metsRepository.GetAsync("b30074976");
        }
        
    }
}