using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights
{
    public class LicenseMap
    {
        private static readonly LicenseMap Instance = new();
        private readonly Dictionary<string, string> dict;
        private readonly Dictionary<string, string> reverse;

        static LicenseMap()
        {
        }

        private LicenseMap()
        {
            // These should match
            // Wellcome.Dds.PlayerConfig.PlayerConfigProvider::GetConditions()
            dict = new Dictionary<string, string>
            {
                ["PDM"] = "https://creativecommons.org/publicdomain/mark/1.0/",
                ["CC0"] = "https://creativecommons.org/publicdomain/zero/1.0/",
                ["CC-BY"] = "https://creativecommons.org/licenses/by/4.0/",
                ["CC-BY-NC"] = "https://creativecommons.org/licenses/by-nc/4.0/",
                ["CC-BY-NC-ND"] = "https://creativecommons.org/licenses/by-nc-nd/4.0/",
                ["CC-BY-ND"] = "https://creativecommons.org/licenses/by-nd/4.0/",
                ["CC-BY-SA"] = "https://creativecommons.org/licenses/by-sa/4.0/",
                ["CC-BY-NC-SA"] = "https://creativecommons.org/licenses/by-nc-sa/4.0/",
                ["OGL"] = "http://www.nationalarchives.gov.uk/doc/open-government-licence/version/2/",
                ["OPL"] = "http://www.parliament.uk/site-information/copyright/open-parliament-licence/",
                ["ARR"] = "https://en.wikipedia.org/wiki/All_rights_reserved",
                ["All Rights Reserved"] = "https://en.wikipedia.org/wiki/All_rights_reserved",
            };

            reverse = dict.Where(kvp => kvp.Key != "ARR").ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        public static string? GetLicenseUri(string licenseAbbreviation) 
            => Instance.dict.TryGetValue(licenseAbbreviation, out string? uri) ? uri : null;
        
        public static string? GetLicenseAbbreviation(string licenseUri) 
            => Instance.reverse.TryGetValue(licenseUri, out string? abbreviation) ? abbreviation : null;

        public static Dictionary<string, string> GetDictionary() => Instance.dict;
    }
}