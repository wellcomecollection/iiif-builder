using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.AssetDomain
{
    public interface IDashboardRepository
    {
        Dictionary<string, long> GetDlcsQueueLevel();
    }
}
