﻿using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class Collection : BaseMetsResource, ICollection
    {
        public Collection(ILogicalStructDiv structDiv)
        {
            Id = structDiv.ExternalId;
            SectionMetadata = structDiv.GetSectionMetadata();
            Label = GetLabel(structDiv, SectionMetadata);
            Type = structDiv.Type;
            Order = structDiv.Order;
            Partial = structDiv.HasChildLink();
            SourceFile = structDiv.WorkStore.GetFileInfoForPath(structDiv.ContainingFileRelativePath); 
        }

        public List<ICollection> Collections { get; set; }
        public List<IManifestation> Manifestations { get; set; }
    }
}
