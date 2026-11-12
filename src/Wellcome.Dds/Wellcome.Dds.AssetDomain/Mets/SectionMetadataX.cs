using Utils;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public static class SectionMetadataX
    {
        /// <summary>
        /// The licence code for a periodical issue. Some MODS data for periodicals is held at the volume level,
        /// so: the issue's own licence if its METS gives one, otherwise the volume's if its METS gives one,
        /// otherwise the default the issue was given.
        /// </summary>
        /// <param name="issue">The MODS for the issue</param>
        /// <param name="volume">The MODS for the volume that contains it, if known</param>
        public static string? GetPeriodicalIssueLicence(this ISectionMetadata issue, ISectionMetadata? volume)
        {
            if (issue.DzLicenseCode.HasText() && !issue.DzLicenseCodeIsDefault)
            {
                return issue.DzLicenseCode;
            }

            if (volume != null && volume.DzLicenseCode.HasText() && !volume.DzLicenseCodeIsDefault)
            {
                return volume.DzLicenseCode;
            }

            return issue.DzLicenseCode;
        }
    }
}
