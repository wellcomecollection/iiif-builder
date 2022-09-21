using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

/// <summary>
/// Contains information about the work and the Manifestation-level metadata.
/// Not so useful at the moment for born digital where the file-level Premis
/// metadata does all the work, but allows structural metadata later and provides
/// metadata that the DDS expects at the manifestation level.
///
/// TODO: Break out the Chemist and Druggist specific parts of this.
/// Need to see whether to keep using this at all for Manifestation DB, aggregation etc.
/// </summary>
public class BornDigitalSectionMetadata : ISectionMetadata
{
    public string Title { get; set; }
    public string DisplayDate { get; set; }
    public string RecordIdentifier { get; set; }
    
    /// <summary>
    /// This is 
    /// </summary>
    public string AccessCondition { get; set; }
    
    /// <summary>
    /// A rights statement
    /// </summary>
    public string DzLicenseCode { get; set; }
    public int PlayerOptions { get; set; }
    public string Usage { get; set; }
    public string Leader6 { get; set; }
    public int VolumeNumber { get; set; }
    public int CopyNumber { get; set; }
    public int PartOrder { get; set; }
    public string Number { get; set; }
    public string GetDisplayTitle() => Title;
}