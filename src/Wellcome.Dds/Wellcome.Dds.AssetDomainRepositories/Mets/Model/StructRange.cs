using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class StructRange : IStructRange
    {
        public string Label { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }
        public List<string> PhysicalFileIds { get; set; }
        public List<IStructRange> Children { get; set; }
        public ISectionMetadata SectionMetadata { get; set; }

        public override string ToString()
        {
            return "[" + Id + "] " + Label + " TYPE=" + Type;
        }
    }
}
