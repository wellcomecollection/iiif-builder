using System;
using System.Collections.Generic;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class Collection : BaseMetsResource, ICollection
    {
        public Collection(ILogicalStructDiv structDiv)
        {
            if (structDiv.ExternalId.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("A Collection logical struct div must have an External ID");
            }
            Identifier = new DdsIdentifier(structDiv.ExternalId);
            SectionMetadata = structDiv.GetSectionMetadata();
            Label = GetLabel(structDiv, SectionMetadata);
            Type = structDiv.Type;
            Order = structDiv.Order;
            Partial = structDiv.HasChildLink();
            SourceFile = structDiv.WorkStore.GetFileInfoForPath(structDiv.ContainingFileRelativePath); 
        }

        public List<ICollection>? Collections { get; set; }
        public List<IManifestation>? Manifestations { get; set; }
    }
}
