using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Frameworks;
using Test.Helpers;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Catalogue;
using Xunit;

namespace CatalogueAPI.Tests
{
    [Trait("Category", "Manual")]
    public class CatalogueTests
    {
        private readonly Wellcome.Dds.Repositories.Catalogue.WellcomeCollectionCatalogue sut;
        private readonly JsonSerializerSettings serializer;

        public CatalogueTests()
        {
            var ddsOptions = new DdsOptions
            {
                ApiWorkTemplate = "https://api.wellcomecollection.org/catalogue/v2/works"
            };

            //httpHandler = new ControllableHttpMessageHandler();
            //var httpClient = new HttpClient(httpHandler);

            var httpClient = new HttpClient();
            var options = Options.Create(ddsOptions);
            sut = new Wellcome.Dds.Repositories.Catalogue.WellcomeCollectionCatalogue(options, httpClient, new NullLogger<WellcomeCollectionCatalogue>());
        }

        [Fact]
        public async Task Can_Get_Result_Page()
        {
            // Arrange
            var identifier = "b14658197";

            // Act
            var resultPage = await sut.GetWorkResultPage(null, identifier);

            // Assert
            resultPage.Results.Length.Should().Be(1);
        }

        [Fact]
        public async Task Can_Pick_Single_Work_When_Multiple_Matches()
        {
            // Arrange
            var identifier = "b14658197";

            // Act
            var work = await sut.GetWorkByOtherIdentifier(identifier);

            // Assert
            work.Id.Should().Be("nydjbrr7");
        }

        [Fact]
        public async Task MoH_Report_Title()
        {
            // Arrange
            var identifier = "b30125285";

            // Act
            var work = await sut.GetWorkByOtherIdentifier(identifier);

            // Assert
            work.Title.Should().Be("[Report 1954] / Medical Officer of Health, St Austell R.D.C.");
        }
        
        
        [Fact]
        public async Task Asked_For_BNumber_Is_Returned()
        {
            // Arrange
            var identifier = "b14658197";

            // Act
            var work = await sut.GetWorkByOtherIdentifier(identifier);

            // Assert
            work.Identifiers.Should().ContainSingle(i => i.Value == identifier);
            var b14658197 = work.Identifiers.Single(i => i.Value == identifier);
            b14658197.IdentifierType.Id.Should().Be("sierra-system-number");
        }
        
        
        [Fact]
        [Trait("Category", "Manual")]
        public async Task Work_Has_Expected_Subjects_And_Genres()
        {
            // Arrange
            var identifier = "b14658197";

            // Act
            var work = await sut.GetWorkByOtherIdentifier(identifier);

            // Assert
            work.Subjects.Should().NotBeEmpty();
            work.Subjects.Should().ContainSingle(s => s.Id == "zp2kkmjt");
            var lcshChemistry = work.Subjects.Single(s => s.Id == "zp2kkmjt");
            lcshChemistry.Identifiers.Should().ContainSingle(i => i.Value == "sh85022996");
            lcshChemistry.Label.Should().Be("Chemistry - Experiments.");
            lcshChemistry.Concepts.Should().ContainSingle(c => c.Label == "Chemistry");
            
            work.Genres.Should().NotBeEmpty();
            work.Genres.Should().ContainSingle(g => g.Label == "Oil paintings");
        }
    }
}
