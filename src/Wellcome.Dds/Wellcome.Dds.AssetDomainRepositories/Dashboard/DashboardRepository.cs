using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public class DashboardRepository : IDashboardRepository
    {
        private ILogger<DashboardRepository> logger;
        private IDlcs dlcs;

        public DashboardRepository(
            ILogger<DashboardRepository> logger,
            IDlcs dlcs)
        {
            this.logger = logger;
            this.dlcs = dlcs;
        }

        public Dictionary<string, long> GetDlcsQueueLevel()
        {
            return dlcs.GetDlcsQueueLevel();
        }
    }
}
