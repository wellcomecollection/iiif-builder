using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Test.Helpers;
using Wellcome.Dds.Common;
using Xunit;

namespace CatalogueAPI.Tests
{
    public class CatalogueTests
    {
        private readonly ControllableHttpMessageHandler httpHandler;
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
            var logger = new NullLogger<Wellcome.Dds.Repositories.Catalogue.WellcomeCollectionCatalogue>();
            sut = new Wellcome.Dds.Repositories.Catalogue.WellcomeCollectionCatalogue(logger, options, httpClient);
            serializer = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        [Fact]
        public async Task Can_Get_Result_Page()
        {
            // Arrange
            var identifier = "b14658197";

            // Act
            var resultPage = await sut.GetWorkResultPage(null, identifier);

            // Assert
            resultPage.Results.Length.Should().Be(8);

        }

        [Fact]
        public async Task MoH_Report_Shape()
        {
            // Arrange
            var identifier = "b30125285";

            //HttpRequestMessage message = null;
            //httpHandler.RegisterCallback(r => message = r);

            // Act
            var work = await sut.GetWork(identifier);

            // Assert
            work.Title.Should().Be("[Report 1954]");
        }
    }
}
