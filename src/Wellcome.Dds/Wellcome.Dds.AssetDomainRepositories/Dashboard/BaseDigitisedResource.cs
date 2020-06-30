using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public abstract class BaseDigitisedResource : IDigitisedResource
    {
        public BNumberModel BNumberModel { get; set; }
        public string Identifier { get; set; }
        public bool? InSyncWithDlcs { get; set; }
        public bool Partial { get; set; }
    }
}
