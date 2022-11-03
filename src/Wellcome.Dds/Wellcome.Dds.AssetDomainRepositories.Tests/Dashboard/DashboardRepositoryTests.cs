using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Dashboard
{
    public class DashboardRepositoryTests
    {
        private readonly IDlcs dlcs;
        private readonly ICatalogue catalogue;
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly DigitalObjectRepository sut;
        
        public DashboardRepositoryTests()
        {
            dlcs = A.Fake<IDlcs>();
            catalogue = A.Fake<ICatalogue>();
            A.CallTo(() => dlcs.DefaultSpace).Returns(999);
            metsRepository = A.Fake<IMetsRepository>();
            
            // WARNING - using in-memory database. This acts differently from Postgres.
            // using here as the queries in sut are very simple so behaviour won't differ across db providers
            ddsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseInMemoryDatabase(nameof(DashboardRepositoryTests)).Options);

            var ddsOptions = new DdsOptions
            {
                LinkedDataDomain = "https://test.com"
            };
            var options = Options.Create(ddsOptions);
            var uriPatterns = new UriPatterns(options);

            sut = new DigitalObjectRepository(new NullLogger<DigitalObjectRepository>(), uriPatterns, dlcs, metsRepository,
                ddsInstrumentationContext);
        }

        [Fact]
        public void DefaultSpace_ExposesDlcsDefaultSpace()
        {
            // This is arranged in ctor as it's set in sut ctor
            // Act
            var defaultSpace = sut.DefaultSpace;
            
            // Assert
            defaultSpace.Should().Be(999);
        }

        [Fact]
        public async Task GetDigitalResource_ThrowsArgumentException_IfMetsRepositoryReturnsNull()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier)).Returns<IMetsResource>(null);
            Func<Task> action = () => sut.GetDigitalObject(identifier);

            // Assert
            (await action.Should().ThrowAsync<ArgumentException>()).And
                .Message.Should().Be(
                    "Cannot get a digital resource from METS for identifier b1231231 (Parameter 'identifier')");
        }

        [Fact]
        public async Task GetDigitalResource_HandlesIManifestation_WithoutPdf()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Identifier = "manifest-id",
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            A.CallTo(() => dlcs.GetImagesForString3("manifest-id")).Returns(images);

            // Act
            var result = (DigitalManifestation)await sut.GetDigitalObject(identifier);

            // Assert
            result.Identifier.Should().Be((DdsIdentifier)"manifest-id");
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesIManifestation_WithPdf()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Identifier = "manifest-id",
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            A.CallTo(() => dlcs.GetImagesForString3("manifest-id")).Returns(images);

            var pdf = new Pdf {Url = "http://example.com/pdf123"};
            A.CallTo(() => dlcs.GetPdfDetails("manifest-id")).Returns(pdf);

            // Act
            var result = (DigitalManifestation)await sut.GetDigitalObject(identifier, true);

            // Assert
            result.Identifier.Should().Be((DdsIdentifier)"manifest-id");
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            result.PdfControlFile.Should().Be(pdf);
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesICollection_WithoutPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            const string identifier = "b1231231";
            var testCollection = new TestCollection
            {
                Identifier = "the-main-one",
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Identifier = "coll-1",
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Identifier = "coll-1-man-1"}
                        }
                    },
                    new TestCollection {Identifier = "coll-2",}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Identifier = "man-1"}
                }
            };
            
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitalCollection)await sut.GetDigitalObject(identifier);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be((DdsIdentifier)"the-main-one");
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == "coll-1" || m.Identifier == "coll-2");
            result.Manifestations.Should().OnlyContain(m => m.Identifier == "man-1");
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == "coll-1-man-1");
            
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesICollection_WithPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            const string identifier = "b1231231";
            var testCollection = new TestCollection
            {
                Identifier = "the-main-one",
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Identifier = "coll-1",
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Identifier = "coll-1-man-1"}
                        }
                    },
                    new TestCollection {Identifier = "coll-2",}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Identifier = "man-1"}
                }
            };
            
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitalCollection)await sut.GetDigitalObject(identifier, true);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be((DdsIdentifier)"the-main-one");
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == "coll-1" || m.Identifier == "coll-2");
            result.Manifestations.Should().OnlyContain(m => m.Identifier == "man-1");
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == "coll-1-man-1");

            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustHaveHappened(2, Times.Exactly);
        }

        [Fact]
        public async Task ExecuteDlcsSyncOperation_Throws_IfDlcsPreventSync()
        {
            // Arrange
            A.CallTo(() => dlcs.PreventSynchronisation).Returns(true);
            Func<Task> action = () => sut.ExecuteDlcsSyncOperation(new SyncOperation(), true);
            
            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteDlcsSyncOperation_MakesRequestsToDlcs_IfPatchAndIngest(bool priority)
        {
            // Arrange
            A.CallTo(() => dlcs.BatchSize).Returns(2);
            var syncOp = new SyncOperation
            {
                DlcsImagesToIngest = Builder<Image>.CreateListOfSize(4).Build().ToList(),
                DlcsImagesToPatch = Builder<Image>.CreateListOfSize(6).Build().ToList(),
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, priority);
            
            // Assert
            A.CallTo(() => dlcs.RegisterImages(A<HydraImageCollection>._, priority)).MustHaveHappened(2, Times.Exactly);
            A.CallTo(() => dlcs.PatchImages(A<HydraImageCollection>._)).MustHaveHappened(3, Times.Exactly);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteDlcsSyncOperation_MakesRequestsToDlcs_IngestOnly(bool priority)
        {
            // Arrange
            A.CallTo(() => dlcs.BatchSize).Returns(10);
            var syncOp = new SyncOperation
            {
                DlcsImagesToIngest = Builder<Image>.CreateListOfSize(4).Build().ToList()
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, priority);
            
            // Assert
            A.CallTo(() => dlcs.RegisterImages(A<HydraImageCollection>._, priority)).MustHaveHappenedOnceExactly();
        }
        
        [Fact]
        public async Task ExecuteDlcsSyncOperation_MakesRequestsToDlcs_PatchOnly()
        {
            // Arrange
            A.CallTo(() => dlcs.BatchSize).Returns(10);
            var syncOp = new SyncOperation
            {
                DlcsImagesToPatch = Builder<Image>.CreateListOfSize(6).Build().ToList(),
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, false);
            
            // Assert
            A.CallTo(() => dlcs.PatchImages(A<HydraImageCollection>._)).MustHaveHappenedOnceExactly();
        }

        public class TestManifestation : IManifestation
        {
            public IArchiveStorageStoredFileInfo SourceFile { get; set; }
            public DdsIdentifier Identifier { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public int? Order { get; set; }
            public ISectionMetadata SectionMetadata { get; set; }
            public ISectionMetadata ParentSectionMetadata { get; set; }
            public bool Partial { get; set; }
            public string GetRootId() => "b12398761";

            public List<IPhysicalFile> Sequence { get; set; }
            public List<IPhysicalFile> SignificantSequence { get; }
            public IStructRange RootStructRange { get; set; }
            public string[] PermittedOperations { get; }
            public string FirstInternetType { get; }
            public List<string> IgnoredStorageIdentifiers { get; }
            public IStoredFile PosterImage { get; set; }
            public List<IStoredFile> SynchronisableFiles { get; }

            public Dictionary<string, IPhysicalFile> PhysicalFileMap { get; set; }
        }
        
        public class TestCollection : ICollection
        {
            public IArchiveStorageStoredFileInfo SourceFile { get; set; }
            public DdsIdentifier Identifier { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public int? Order { get; }
            public ISectionMetadata SectionMetadata { get; }
            public ISectionMetadata ParentSectionMetadata { get; }
            public bool Partial { get; set; }
            public string GetRootId() => "b12398761";

            public List<ICollection> Collections { get; set; }
            public List<IManifestation> Manifestations { get; set; }
        }
    }
}
