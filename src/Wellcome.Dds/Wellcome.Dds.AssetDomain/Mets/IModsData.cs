namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IModsData
    {
        string Title { get; set; }
        string SubTitle { get; set; }
        string Classification { get; set; }
        string Language { get; set; }
        string OriginDateDisplay { get; set; }
        string OriginPlace { get; set; }
        string OriginPublisher { get; set; }
        string PhysicalDescription { get; set; }
        string DisplayForm { get; set; }
        string RecordIdentifier { get; set; }
        IModsName[] Names { get; set; }
        string Identifier { get; set; }
        string AccessCondition { get; set; }
        string DzLicenseCode { get; set; }
        int PlayerOptions { get; set; }
        string Usage { get; set; }
        string Leader6 { get; set; }
        string WellcomeIdentifier { get; set; }
        string RepositoryName { get; set; }
        int VolumeNumber { get; set; }
        int CopyNumber { get; set; }
        
        // These two introduced for Periodicals.
        // They are specific to their observed use in Chemist and Druggist;
        // the could be generalised.
        int PartOrder { get; set; }
        string Number { get; set; }

        string GetDisplayTitle();
        IModsData GetDeepCopyForAccessControl();
    }

}
