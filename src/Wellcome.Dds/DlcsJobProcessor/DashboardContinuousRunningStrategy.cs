using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;

namespace DlcsJobProcessor
{
    public class DashboardContinuousRunningStrategy : BackgroundService
    {
        private readonly ILogger<DashboardContinuousRunningStrategy> logger;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly JobProcessorOptions options;

        public DashboardContinuousRunningStrategy(
            ILogger<DashboardContinuousRunningStrategy> logger,
            IOptions<JobProcessorOptions> options,
            IServiceScopeFactory serviceScopeFactory)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            this.options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Running DashboardContinuousRunningStrategy");

            var fromSeconds = TimeSpan.FromSeconds(options.YieldTimeSecs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var statusProvider = scope.ServiceProvider.GetRequiredService<IStatusProvider>();
                    if (!await statusProvider.ShouldRunProcesses(stoppingToken))
                    {
                        logger.LogWarning("Status provider returned false, will not attempt to process");
                    }
                    else
                    {
                        
                        var processor = scope.ServiceProvider.GetRequiredService<IIngestJobProcessor>();

                        switch (options.Mode)
                        {
                            case "processqueue":
                                await statusProvider.WriteHeartbeat(stoppingToken);
                                await processor.ProcessQueue(-1, false, options.Filter);
                                break;
                            case "updatestatus":
                                processor.UpdateStatus();
                                break;
                            default:
                                logger.LogWarning("'{mode}' is not a known command. Aborting", options.Mode);
                                return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error running DashboardContinuousRunningStrategy");
                }
                
                await Task.Delay(fromSeconds, stoppingToken);
            }
            
            logger.LogInformation("Stopping DashboardContinuousRunningStrategy...");
        }
    }
}
