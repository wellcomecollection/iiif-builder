using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain
{
    public interface IDashboardRepository
    {
        Dictionary<string, long> GetDlcsQueueLevel();
    }
}
