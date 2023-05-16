using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

public class PremisRightsStatement : IRightsStatement
{
    public string? Identifier { get; set; }
    public string? Basis { get; set; }
    public string? AccessCondition { get; set; }
    public string? Status { get; set; }
    public string? Statement { get; set; }
}