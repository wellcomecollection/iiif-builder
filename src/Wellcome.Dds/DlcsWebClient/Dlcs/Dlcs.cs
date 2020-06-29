using DlcsWebClient.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace DlcsWebClient.Dlcs
{
    public class Dlcs : IDlcs
    {
        private ILogger<Dlcs> logger;
        private DlcsOptions options;

        public Dlcs(
            ILogger<Dlcs> logger,
            IOptions<DlcsOptions> options)
        {
            this.logger = logger;
            this.options = options.Value;
        }

        public Dictionary<string, long> GetDlcsQueueLevel()
        {
            string key = $"queue-{options.CustomerName}";
            return new Dictionary<string, long> { [key] = options.CustomerId };
        }
    }
}
