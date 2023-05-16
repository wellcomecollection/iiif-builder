namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights
{
    public static class LicenceCodes
    {
        public static string? MapLicenseCode(string? dzLicenseCode)
        {
            switch (dzLicenseCode)
            {
                case "S":
                    return "PDM";
                case "B":
                case "R":
                case "O":
                    return "CC-BY";
                case "A":
                case "C":
                case "J":
                case "L":
                    return "CC-BY-NC";
                case "E":
                    return "CC-BY-ND";
                case "F":
                    return "CC-BY-SA";
                case "D":
                case "K":
                case "M":
                    return "CC-BY-NC-ND";
                case "G":
                    return "CC-BY-NC-SA";
                case "H":
                    return "OGL";
                case "I":
                    return "OPL";
                default:
                    // unknown, return the code as-is
                    return dzLicenseCode;
            }
        }
    }
}