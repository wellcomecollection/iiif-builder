using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.AssetDomain.Dlcs
{
    public interface IDlcs
    {
        Dictionary<string, long> GetDlcsQueueLevel();
    }
}
