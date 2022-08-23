﻿using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public abstract class BaseDigitalObject : IDigitalObject
    {
        public string Identifier { get; set; }
        public bool? InSyncWithDlcs { get; set; }
        public bool Partial { get; set; }
    }
}
