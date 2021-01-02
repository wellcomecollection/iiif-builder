using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.AssetDomainRepositories.Storage.FileSystem;
using Xunit;

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
            b16675630_0001.Should().BeOfType<Wellcome.Dds.AssetDomainRepositories.Mets.Model.Manifestation>();
            var mb16675630_0001 = (Wellcome.Dds.AssetDomainRepositories.Mets.Model.Manifestation) b16675630_0001;
            mb16675630_0001.Partial.Should().BeFalse();
            mb16675630_0001.PosterImage.RelativePath.Should().Be("posters/0055-0000-3718-0000-0-0000-0000-0.jpg");
        }
        
    }
}