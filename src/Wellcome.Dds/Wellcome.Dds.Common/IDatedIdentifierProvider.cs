using System;
using System.Collections.Generic;

namespace Wellcome.Dds.Common
{
    public interface IDatedIdentifierProvider
    {
        List<DatedIdentifier> GetDatedIdentifiers(DateTime @from, DateTime? to);
        List<DatedIdentifier> GetDatedIdentifiers(int count);
    }
}
