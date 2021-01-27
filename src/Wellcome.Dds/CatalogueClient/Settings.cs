using System.IO;

namespace CatalogueClient
{
    /// <summary>
    /// Nothing fancy, just some hard-coded values for now
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// The dump of all records, refreshed daily
        /// </summary>
        public static string CatalogueDump = "https://data.wellcomecollection.org/catalogue/v2/works.json.gz";
        
        /// <summary>
        /// Where to put dump downloads
        /// </summary>
        public static string LocalDumpPath = Path.Combine(Path.GetTempPath(), "dump.gz");
        
        
        /// <summary>
        /// Where to put expanded data
        /// </summary>
        public static string LocalExpandedPath = Path.Combine(Path.GetTempPath(), "dump.txt");
    }
}