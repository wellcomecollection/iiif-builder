
using Utils;

namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights
{
    public class LicenceHelpers
    {
        public static string? GetUsageWithHtmlLinks(string? rawUsage)
        {
            if (string.IsNullOrWhiteSpace(rawUsage))
            {
                return null;
            }
            const string template = "<a href=\"{1}\">{0}</a>";
            return rawUsage.ReplaceFromDictionary(LicenseMap.GetDictionary(), template);
        }
    }
}