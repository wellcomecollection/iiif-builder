using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Control;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    /// <summary>
    /// Implementation of <see cref="IStatusProvider"/> using db for backing store.
    /// </summary>
    public class DatabaseStatusProvider : IStatusProvider
    {
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly ILogger<DatabaseStatusProvider> logger;
        private readonly DdsOptions ddsOptions;
        
        public DatabaseStatusProvider(
            IOptions<DdsOptions> ddsOptions,
            DdsInstrumentationContext ddsInstrumentationContext,
            ILogger<DatabaseStatusProvider> logger)
        {
            this.ddsInstrumentationContext = ddsInstrumentationContext;
            this.logger = logger;
            this.ddsOptions = ddsOptions.Value;
        }
        
        public async Task<bool> ShouldRunProcesses(CancellationToken cancellationToken = default)
        {
            var currentControlFlow = await GetLatestControlFlow(cancellationToken, true);
            return currentControlFlow.StoppedOn == null;
        }

        public DateTime? EarliestJobToTake => StringUtils.GetNullableDateTime(ddsOptions.EarliestJobDateTime);

        public DateTime? LatestJobToTake
        {
            get
            {
                if (ddsOptions.MinimumJobAgeMinutes > 0)
                {
                    return DateTime.Now.AddMinutes(-ddsOptions.MinimumJobAgeMinutes);
                }

                return null;
            }
        }

        public async Task<bool> Stop(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentControlFlow = await GetLatestControlFlow(cancellationToken);
                currentControlFlow.StoppedOn = DateTime.Now;
                await ddsInstrumentationContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Stopping control_flow");
                return false;
            }
        }

        public async Task<bool> Start(CancellationToken cancellationToken = default)
        {
            try
            {
                var controlFlow = new ControlFlow
                {
                    CreatedOn = DateTime.Now
                };
                await ddsInstrumentationContext.ControlFlows.AddAsync(controlFlow, cancellationToken);
                await ddsInstrumentationContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Starting control_flow");
                return false;
            }
        }

        public async Task<DateTime?> WriteHeartbeat(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentControlFlow = await GetLatestControlFlow(cancellationToken);
                currentControlFlow.Heartbeat = DateTime.Now;
                await ddsInstrumentationContext.SaveChangesAsync(cancellationToken);
                return currentControlFlow.Heartbeat;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting heartbeat for control_flow");
                return null;
            }
        }

        public async Task<DateTime?> GetHeartbeat(CancellationToken cancellationToken = default)
        {
            var currentControlFlow = await GetLatestControlFlow(cancellationToken, true);
            return currentControlFlow.Heartbeat;
        }
        
        private async Task<ControlFlow> GetLatestControlFlow(CancellationToken cancellationToken, bool reload = false)
        {
            var currentControlFlow = await ddsInstrumentationContext.ControlFlows
                .OrderByDescending(cf => cf.Id)
                .FirstAsync(cancellationToken);

            if (reload)
            {
                await ddsInstrumentationContext.Entry(currentControlFlow).ReloadAsync(cancellationToken);
            }

            return currentControlFlow;
        }
    }
}