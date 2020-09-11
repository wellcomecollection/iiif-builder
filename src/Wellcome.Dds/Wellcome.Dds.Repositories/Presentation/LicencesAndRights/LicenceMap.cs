using System.Collections.Generic;

namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights
{
    public class LicenseMap
    {
        private static readonly LicenseMap Instance = new LicenseMap();
        private readonly Dictionary<string, string> dict;

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
                ["ARR"] = "https://en.wikipedia.org/wiki/All_rights_reserved "
            };
        }

        public static string GetLicenseUri(string licenseAbbreviation)
        {
            if (Instance.dict.ContainsKey(licenseAbbreviation))
            {
                return Instance.dict[licenseAbbreviation];
            }
            return null;
        }

        public static Dictionary<string, string> GetDictionary()
        {
            return Instance.dict;
        } 
    }
}