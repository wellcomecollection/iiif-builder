using System;

namespace Wellcome.Dds.AssetDomain.Mets;

public interface IRightsStatement
{
    /// <summary>
    /// The identifier of this particular rights statement within a digital object
    /// </summary>
    public string? Identifier { get; set; }
    
    /// <summary>
    /// On what grounds is this statement being asserted? e.g., copyright, licensing
    /// </summary>
    public string? Basis { get; set; }
    
    /// <summary>
    /// The Access Condition, in DDS terms - e.g., "Open", "Restricted"
    /// In born digital modelling this is held in rightsGrantedNote
    /// </summary>
    public string? AccessCondition { get; set; }
    
    /// <summary>
    /// This holds the value of the license code or copyright status, e.g., "CC-BY" or "In copyright"
    /// (licenseNote or copyrightNote in PREMIS)
    /// </summary>
    public string? Statement { get; set; }
    
    /// <summary>
    /// copyrightStatus if copyright is the basis
    /// </summary>
    public string? Status { get; set; }
}