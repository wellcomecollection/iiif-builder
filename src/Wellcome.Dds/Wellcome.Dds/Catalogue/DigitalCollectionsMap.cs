using System.Collections.Generic;

namespace Wellcome.Dds.Catalogue
{
    public class DigitalCollectionsMap
    {
        private static readonly Dictionary<string, string> Lookups = new()
        {
            {"digaids", "AIDS posters"},
            {"digramc", "Royal Army Medical Corps archives"},
            {"diggenetics", "Genetics books and archives"},
            {"digmoh", "Medical Officer of Health reports"},
            {"digsexology", "Sexology"},
            {"digasylum", "Mental healthcare"},
            {"digarabic", "Arabic manuscripts"},
            {"digbiomed", "Biomedical images"},
            {"digrecipe", "Recipe book manuscripts"},
            {"digwms", "Medieval and Early Modern manuscripts"},
            {"digukmhl", "UK Medical Heritage Library"}
        };

        public static string GetFriendlyName(string collectionCode)
        {
            return Lookups.ContainsKey(collectionCode) ? Lookups[collectionCode] : null;
        }
    }
}