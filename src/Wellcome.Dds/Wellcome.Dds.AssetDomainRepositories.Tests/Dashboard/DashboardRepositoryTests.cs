using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FakeItEasy;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Dlcs.RestOperations;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Xunit;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Dashboard
{
    public class DashboardRepositoryTests
    {
        private readonly IDlcs dlcs;
        private readonly ICatalogue catalogue;
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly DigitalObjectRepository sut;
        private readonly IIdentityService identityService;
        
        public DashboardRepositoryTests()
        {
            dlcs = A.Fake<IDlcs>();
            catalogue = A.Fake<ICatalogue>();
            A.CallTo(() => dlcs.DefaultSpace).Returns(999);
            A.CallTo(() => dlcs.RegisterImages(
                    A<HydraImageCollection>._, 
                    A<DlcsCallContext>._, 
                    A<bool>._))
                .Returns(new Operation<HydraImageCollection, Batch>(new Uri("https://fake.io"), HttpMethod.Post));
            A.CallTo(() => dlcs.PatchImages(
                    A<HydraImageCollection>._, 
                    A<DlcsCallContext>._))
                .Returns(new Operation<HydraImageCollection, HydraImageCollection>(new Uri("https://fake.io"), HttpMethod.Post));
            metsRepository = A.Fake<IMetsRepository>();
            
            // WARNING - using in-memory database. This acts differently from Postgres.
            // using here as the queries in sut are very simple so behaviour won't differ across db providers
            ddsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseInMemoryDatabase(nameof(DashboardRepositoryTests)).Options,
                new NullLogger<DdsInstrumentationContext>());

            var ddsOptions = new DdsOptions
            {
                LinkedDataDomain = "https://test.com",
                WellcomeCollectionApi = "(unused in this test)",
                ApiWorkTemplate = "(unused in this test)"
            };
            var options = Options.Create(ddsOptions);
            var uriPatterns = new UriPatterns(options);
            
            identityService = new ParsingIdentityService(new NullLogger<ParsingIdentityService>(), new MemoryCache(new MemoryCacheOptions()));
            sut = new DigitalObjectRepository(new NullLogger<DigitalObjectRepository>(), uriPatterns, dlcs, metsRepository,
                ddsInstrumentationContext, options, identityService);
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
            var identifier = identityService.GetIdentity("b1231231");
            A.CallTo(() => metsRepository.GetAsync(identifier)).Returns<IMetsResource>(null);
            Func<Task> action = () => sut.GetDigitalObject(identifier.PackageIdentifier, new DlcsCallContext("[test]", "[id]"));

            // Assert
            (await action.Should().ThrowAsync<ArgumentException>()).And
                .Message.Should().Be(
                    "Cannot get a digital resource from METS for identifier b1231231 (Parameter 'identifier')");
        }

        [Fact]
        public async Task GetDigitalResource_HandlesIManifestation_WithoutPdf()
        {
            // Arrange
            var idString = "b1231231"; // This can be anything in this test
            var identifier = identityService.GetIdentity(idString); 
            var testIdentity = identityService.GetIdentity("manifest-id"); 
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Identifier = testIdentity,
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var ctx = new DlcsCallContext("[test]", "[id]");
            A.CallTo(() => dlcs.GetImagesForString3("manifest-id", ctx)).Returns(images);

            // Act
            var result = (DigitalManifestation)await sut.GetDigitalObject(idString, ctx);

            // Assert
            result.Identifier.Should().Be(testIdentity);
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesIManifestation_WithPdf()
        {
            // Arrange
            var idString = "b1231231"; // This can be anything in this test
            var identifier = identityService.GetIdentity(idString); 
            var testIdentity = identityService.GetIdentity("manifest-id"); 
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(new TestManifestation
                {
                    Identifier = testIdentity,
                    Partial = true,
                    Label = "foo"
                });
            var images = Builder<Image>.CreateListOfSize(3).Build().ToArray();
            var ctx = new DlcsCallContext("[test]", "[id]");
            A.CallTo(() => dlcs.GetImagesForString3("manifest-id", ctx)).Returns(images);

            var pdf = new Pdf {Url = "http://example.com/pdf123"};
            A.CallTo(() => dlcs.GetPdfDetails("manifest-id")).Returns(pdf);

            // Act
            var result = (DigitalManifestation)await sut.GetDigitalObject(idString, ctx, true);

            // Assert
            result.Identifier.Should().Be(testIdentity);
            result.Partial.Should().BeTrue();
            result.DlcsImages.Should().BeEquivalentTo(images);
            result.PdfControlFile.Should().Be(pdf);
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesICollection_WithoutPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            var idString = "b1231231"; // This can be anything in this test
            var identifier = identityService.GetIdentity(idString); 
            var theMainOne = identityService.GetIdentity("the-main-one"); 
            var coll1 = identityService.GetIdentity("coll-1"); 
            var coll1Man1 = identityService.GetIdentity("coll-1-man-1"); 
            var coll2 = identityService.GetIdentity("coll-2"); 
            var man1 = identityService.GetIdentity("man-1"); 
            var testCollection = new TestCollection
            {
                Identifier = theMainOne,
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Identifier = coll1,
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Identifier = coll1Man1}
                        }
                    },
                    new TestCollection {Identifier = coll2,}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Identifier = man1}
                }
            };

            var ctx = new DlcsCallContext("[test]", "[id]");
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._, ctx))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitalCollection)await sut.GetDigitalObject(idString, ctx);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be(theMainOne);
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == coll1 || m.Identifier == coll2);
            result.Manifestations.Should().OnlyContain(m => m.Identifier == man1);
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == coll1Man1);
            
            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetDigitalResource_HandlesICollection_WithPdf()
        {
            // NOTE: These aren't exhaustive but verify format of return value
            // Arrange
            var idString = "b1231231"; // This can be anything in this test
            var identifier = identityService.GetIdentity(idString); 
            var theMainOne = identityService.GetIdentity("the-main-one"); 
            var coll1 = identityService.GetIdentity("coll-1"); 
            var coll1Man1 = identityService.GetIdentity("coll-1-man-1"); 
            var coll2 = identityService.GetIdentity("coll-2"); 
            var man1 = identityService.GetIdentity("man-1"); 
            var testCollection = new TestCollection
            {
                Identifier = theMainOne,
                Partial = true,
                Collections = new List<ICollection>
                {
                    new TestCollection
                    {
                        Identifier = coll1,
                        Manifestations = new List<IManifestation>
                        {
                            new TestManifestation {Identifier = coll1Man1}
                        }
                    },
                    new TestCollection {Identifier = coll2,}
                },
                Manifestations = new List<IManifestation>
                {
                    new TestManifestation {Identifier = man1}
                }
            };

            var ctx = new DlcsCallContext("[test]", "[id]");
            A.CallTo(() => metsRepository.GetAsync(identifier))
                .Returns(testCollection);
            A.CallTo(() => dlcs.GetImagesForString3(A<string>._, ctx))
                .Returns(Builder<Image>.CreateListOfSize(2).Build().ToArray());

            // Act
            var result = (DigitalCollection)await sut.GetDigitalObject(idString, ctx,true);

            // Assert
            result.MetsCollection.Should().Be(testCollection);
            result.Identifier.Should().Be(theMainOne);
            result.Partial.Should().BeTrue();
            result.Collections.Should().OnlyContain(m => m.Identifier == coll1 || m.Identifier == coll2);
            result.Manifestations.Should().OnlyContain(m => m.Identifier == man1);
            
            var nestedColl = result.Collections.First();
            nestedColl.Collections.Should().BeNullOrEmpty();
            nestedColl.Manifestations.Should().OnlyContain(m => m.Identifier == coll1Man1);

            A.CallTo(() => dlcs.GetPdfDetails(A<string>._)).MustHaveHappened(2, Times.Exactly);
        }

        [Fact]
        public async Task ExecuteDlcsSyncOperation_Throws_IfDlcsPreventSync()
        {
            // Arrange
            A.CallTo(() => dlcs.PreventSynchronisation).Returns(true);
            var ctx = new DlcsCallContext("[test]", "[id]");
            var syncOp = new SyncOperation(ctx);
            Func<Task> action = () => sut.ExecuteDlcsSyncOperation(syncOp, true, ctx);
            
            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
        
        
        [Fact]
        public async Task ExecuteDlcsSyncOperation_Throws_IfMissingAccessConditions()
        {
            // Arrange
            var pf = new PhysicalFile(A.Fake<IWorkStore>(), "test")
            {
                AccessCondition = AccessCondition.Missing
            };
            var sf = new StoredFile { PhysicalFile = pf };
            var ctx = new DlcsCallContext("[test]", "[id]");
            var syncOp = new SyncOperation(ctx);
            syncOp.MissingAccessConditions = new List<IStoredFile> { sf };
            Func<Task> action = () => sut.ExecuteDlcsSyncOperation(syncOp, true, ctx);
            
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
            var ctx = new DlcsCallContext("[test]", "[id]");
            var syncOp = new SyncOperation(ctx)
            {
                DlcsImagesToIngest = Builder<Image>.CreateListOfSize(4).Build().ToList(),
                DlcsImagesToPatch = Builder<Image>.CreateListOfSize(6).Build().ToList(),
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, priority, ctx);
            
            // Assert
            A.CallTo(() => dlcs.RegisterImages(A<HydraImageCollection>._, ctx, priority)).MustHaveHappened(2, Times.Exactly);
            A.CallTo(() => dlcs.PatchImages(A<HydraImageCollection>._, ctx)).MustHaveHappened(3, Times.Exactly);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteDlcsSyncOperation_MakesRequestsToDlcs_IngestOnly(bool priority)
        {
            // Arrange
            A.CallTo(() => dlcs.BatchSize).Returns(10);
            var ctx = new DlcsCallContext("[test]", "[id]");
            var syncOp = new SyncOperation(ctx)
            {
                DlcsImagesToIngest = Builder<Image>.CreateListOfSize(4).Build().ToList()
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, priority, ctx);
            
            // Assert
            A.CallTo(() => dlcs.RegisterImages(A<HydraImageCollection>._, ctx, priority)).MustHaveHappenedOnceExactly();
        }
        
        [Fact]
        public async Task ExecuteDlcsSyncOperation_MakesRequestsToDlcs_PatchOnly()
        {
            // Arrange
            A.CallTo(() => dlcs.BatchSize).Returns(10);
            var ctx = new DlcsCallContext("[test]", "[id]");
            var syncOp = new SyncOperation(ctx)
            {
                DlcsImagesToPatch = Builder<Image>.CreateListOfSize(6).Build().ToList(),
            };
            
            // Act
            await sut.ExecuteDlcsSyncOperation(syncOp, false, ctx);
            
            // Assert
            A.CallTo(() => dlcs.PatchImages(A<HydraImageCollection>._, ctx)).MustHaveHappenedOnceExactly();
        }

        public class TestManifestation : IManifestation
        {
            public IArchiveStorageStoredFileInfo SourceFile { get; set; }
            public DdsIdentity Identifier { get; set; }
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
            public DdsIdentity Identifier { get; set; }
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
