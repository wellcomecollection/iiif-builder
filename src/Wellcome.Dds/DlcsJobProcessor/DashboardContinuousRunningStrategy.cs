using DlcsWebClient.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DlcsJobProcessor
{
    public class DashboardContinuousRunningStrategy : IHostedService
    {
        private ILogger<DashboardContinuousRunningStrategy> logger;
        private DlcsOptions options;

        public DashboardContinuousRunningStrategy(
            ILogger<DashboardContinuousRunningStrategy> logger,
            IOptions<DlcsOptions> options)
        {
            this.logger = logger;
            this.options = options.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Started DashboardContinuousRunningStrategy");
            logger.LogInformation("Customer is {0}", options.CustomerName);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping DashboardContinuousRunningStrategy");
            return Task.CompletedTask;
        }
    }
}
