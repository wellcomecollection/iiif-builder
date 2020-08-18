﻿using System;
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
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Common;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Dashboard
{
    public class DashboardRepositoryTests
    {
        private readonly IDlcs dlcs;
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly DashboardRepository sut;
        
        public DashboardRepositoryTests()
        {
            dlcs = A.Fake<IDlcs>();
            A.CallTo(() => dlcs.DefaultSpace).Returns(999);
            metsRepository = A.Fake<IMetsRepository>();
            
            // WARNING - using in-memory database. This acts differently from Postgres.
            // using here as the queries in sut are very simple so behaviour won't differ across db providers
            ddsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseInMemoryDatabase(nameof(DashboardRepositoryTests)).Options);

            var ddsOptions = new DdsOptions
            {
                PersistentCatalogueRecord = "/{0}/catalogue",
                PersistentPlayerUri = "/test/{0}/player/x",
                ManifestTemplate = "/manifest/{0}",
                EncoreBibliographicData = "/biblio/{0}"
            };
            var options = Options.Create(ddsOptions);

            sut = new DashboardRepository(new NullLogger<DashboardRepository>(), options, dlcs, metsRepository,
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
        public void GetBNumberModel_ReturnsExpected()
        {
            // Arrange
            const string bNumber = "b123456789";
            const string label = "foo";

            var expected = new BNumberModel
            {
                BNumber = bNumber,
                DisplayTitle = label,
                EncoreRecordUrl = "/b1234567/catalogue",
                ItemPageUrl = "/test/b123456789/player/x",
                ManifestUrl = "/manifest/b123456789",
                EncoreBiblioRecordUrl = "/biblio/b1234567"
            };
            
            // Act
            var bNumberModel = sut.GetBNumberModel(bNumber, label);
            
            // Assert
            bNumberModel.Should().BeEquivalentTo(expected);
        }

        [Theory(Skip = "Verify this is expected behaviour")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GetBNumberModel_Throws_IfBNumberNullOrWhitespace(string bNumber)
        {
            // Arrange
            Action action = () => sut.GetBNumberModel(bNumber, "foo");
            
            // Assert    
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetDigitisedResource_ThrowsArgumentException_IfMetsRepositoryReturnsNull()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier)).Returns<IMetsResource>(null);
            Func<Task> action = () => sut.GetDigitisedResource(identifier);

            // Assert
            action.Should().Throw<ArgumentException>().And
                .Message.Should().Be(
                    "Cannot get a digitised resource from METS for identifier b1231231 (Parameter 'identifier')");
        }

        [Fact]
        public async Task GetDigitisedResource_HandlesIManifestation_WithoutPdf()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Id = "manifest_id",
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            A.CallTo(() => dlcs.GetImagesForString3("manifest_id")).Returns(images);

            // Act
            var result = (DigitisedManifestation)await sut.GetDigitisedResource(identifier);

            // Assert
            result.Identifier.Should().Be("manifest_id");
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitisedResource_HandlesIManifestation_WithPdf()
        {
            // Arrange
            const string identifier = "b1231231";
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Id = "manifest_id",
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            A.CallTo(() => dlcs.GetImagesForString3("manifest_id")).Returns(images);

            var pdf = new Pdf {Url = "http://example.com/pdf123"};
            A.CallTo(() => dlcs.GetPdfDetails("manifest_id")).Returns(pdf);

            // Act
            var result = (DigitisedManifestation)await sut.GetDigitisedResource(identifier, true);

            // Assert
            result.Identifier.Should().Be("manifest_id");
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            result.PdfControlFile.Should().Be(pdf);
        }
        
        [Fact]
        public async Task GetDigitisedResource_HandlesICollection_WithoutPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            const string identifier = "b1231231";
            var testCollection = new TestCollection
            {
                Id = "the_main_one",
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Id = "coll_1",
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Id = "coll_1_man_1"}
                        }
                    },
                    new TestCollection {Id = "coll_2",}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Id = "man_1"}
                }
            };
            
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitisedCollection)await sut.GetDigitisedResource(identifier);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be("the_main_one");
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == "coll_1" || m.Identifier == "coll_2");
            result.Manifestations.Should().OnlyContain(m => m.Identifier == "man_1");
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == "coll_1_man_1");
            
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitisedResource_HandlesICollection_WithPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            const string identifier = "b1231231";
            var testCollection = new TestCollection
            {
                Id = "the_main_one",
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Id = "coll_1",
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Id = "coll_1_man_1"}
                        }
                    },
                    new TestCollection {Id = "coll_2",}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Id = "man_1"}
                }
            };
            
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitisedCollection)await sut.GetDigitisedResource(identifier, true);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be("the_main_one");
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == "coll_1" || m.Identifier == "coll_2");
            result.Manifestations.Should().OnlyContain(m => m.Identifier == "man_1");
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == "coll_1_man_1");

            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustHaveHappened(2, Times.Exactly);
        }

        [Fact]
        public void ExecuteDlcsSyncOperation_Throws_IfDlcsPreventSync()
        {
            // Arrange
            A.CallTo(() => dlcs.PreventSynchronisation).Returns(true);
            Func<Task> action = () => sut.ExecuteDlcsSyncOperation(new SyncOperation(), true);
            
            // Assert
            action.Should().Throw<InvalidOperationException>();
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
            public string Id { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public int? Order { get; set; }
            public IModsData ModsData { get; set; }
            public IModsData ParentModsData { get; set; }
            public bool Partial { get; set; }
            public string GetRootId() => "b12398761";

            public List<IPhysicalFile> Sequence { get; set; }
            public List<IPhysicalFile> SignificantSequence { get; }
            public IStructRange RootStructRange { get; set; }
            public string[] PermittedOperations { get; }
            public string FirstSignificantInternetType { get; }
            public List<string> IgnoredStorageIdentifiers { get; }
            public IStoredFile PosterImage { get; set; }
        }
        
        public class TestCollection : ICollection
        {
            public IArchiveStorageStoredFileInfo SourceFile { get; set; }
            public string Id { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public int? Order { get; }
            public IModsData ModsData { get; }
            public IModsData ParentModsData { get; }
            public bool Partial { get; set; }
            public string GetRootId() => "b12398761";

            public List<ICollection> Collections { get; set; }
            public List<IManifestation> Manifestations { get; set; }
        }
    }
}
