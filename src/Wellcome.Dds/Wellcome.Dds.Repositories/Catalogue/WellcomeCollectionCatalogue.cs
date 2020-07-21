using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Catalogue
{
    public class WellcomeCollectionCatalogue : ICatalogue
    {
        private ILogger<WellcomeCollectionCatalogue> logger;
        private DdsOptions options;
        private readonly HttpClient httpClient;

        public WellcomeCollectionCatalogue(
            ILogger<WellcomeCollectionCatalogue> logger,
            IOptions<DdsOptions> ddsOptions,
            HttpClient httpClient)
        {
            this.logger = logger;
            options = ddsOptions.Value;
            this.httpClient = httpClient;
        }

        public Work GetWork(string identifier)
        {
            throw new NotImplementedException();
        }
    }
}
