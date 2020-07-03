using System;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    [Serializable]
    public class ModsName : IModsName
    {
        public string DisplayName { get; set; }
        public string FamilyName { get; set; }
        public string GivenName { get; set; }
        public string NameType { get; set; }
        public string Role { get; set; }
    }
}
