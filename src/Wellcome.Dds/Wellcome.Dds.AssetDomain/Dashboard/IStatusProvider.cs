using System;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IStatusProvider
    {
        bool RunProcesses { get; }
        DateTime? EarliestJobToTake { get; }

        DateTime? LatestJobToTake { get; }

        bool Stop();
        bool Start();

        DateTime? WriteHeartbeat();
        DateTime? GetHeartbeat();

        bool LogSpecial(string message);
    }
}
