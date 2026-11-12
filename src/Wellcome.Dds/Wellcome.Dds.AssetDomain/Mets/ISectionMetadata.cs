namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// Provided by MODS for Goobi METS and *currently* by Premis file level metadata
    /// for Archivematica born digital METS.
    ///
    /// If Archivematica METS starts to use metadata attached to structure (e.g., for a folder)
    /// then this class will be more useful.
    /// </summary>
    public interface ISectionMetadata
    {
        string? Title { get; set; }
        string? DisplayDate { get; set; }
        string? RecordIdentifier { get; set; }
        string? AccessCondition { get; set; }
        string? DzLicenseCode { get; set; }
        int PlayerOptions { get; set; }
        string? Usage { get; set; }
        string? Leader6 { get; set; }
        int VolumeNumber { get; set; }
        int CopyNumber { get; set; }
        
        // These two introduced for Periodicals.
        // They are specific to their observed use in Chemist and Druggist;
        // the could be generalised.
        int PartOrder { get; set; }
        string? Number { get; set; }

        string? GetDisplayTitle();
    }

}
