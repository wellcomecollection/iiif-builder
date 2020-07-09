using System;
using DlcsWebClient.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace DlcsJobProcessor
{
    public class DashboardContinuousRunningStrategy : BackgroundService
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Running DashboardContinuousRunningStrategy");
            logger.LogInformation("Customer is {0}", options.CustomerName);
            var count = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Loop {count}...", ++count);
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
