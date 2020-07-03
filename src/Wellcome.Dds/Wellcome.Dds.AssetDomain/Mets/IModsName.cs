namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IModsName
    {
        string DisplayName { get; set; }
        string FamilyName { get; set; }
        string GivenName { get; set; }
        string NameType { get; set; }
        string Role { get; set; }
    }
}
