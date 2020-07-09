using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkflowProcessor
{
    public class WorkflowProcessorService : BackgroundService
    {
        private readonly ILogger<WorkflowProcessorService> logger;
        
        // NOTE - to handle SIGTERM inject IHostApplicationLifetime and register
        // hostApplicationLifetime.ApplicationStopping.Register(OnStopping);

        public WorkflowProcessorService(ILogger<WorkflowProcessorService> logger)
        {
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Hosted service ExecuteAsync");
            var count = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Loop {count}...", ++count);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}