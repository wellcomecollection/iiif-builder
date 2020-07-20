using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IStatusProvider
    {
        Task<bool> ShouldRunProcesses(CancellationToken cancellationToken = default);
        
        DateTime? EarliestJobToTake { get; }

        DateTime? LatestJobToTake { get; }

        Task<bool> Stop(CancellationToken cancellationToken = default);
        
        Task<bool> Start(CancellationToken cancellationToken = default);

        Task<DateTime?> WriteHeartbeat(CancellationToken cancellationToken = default);
        
        Task<DateTime?> GetHeartbeat(CancellationToken cancellationToken = default);

        bool LogSpecial(string message);
    }
}
