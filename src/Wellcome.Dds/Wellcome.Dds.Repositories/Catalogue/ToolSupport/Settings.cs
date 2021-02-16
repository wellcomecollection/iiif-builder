using System.IO;

namespace Wellcome.Dds.Repositories.Catalogue.ToolSupport
{
    /// <summary>
    /// Nothing fancy, just some hard-coded values for now
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// The dump of all records, refreshed daily
        /// </summary>
        public const string CatalogueDump = "https://data.wellcomecollection.org/catalogue/v2/works.json.gz";

        /// <summary>
        /// Where to put dump downloads
        /// </summary>
        public static readonly string LocalDumpPath = Path.Combine(Path.GetTempPath(), "dump.gz");
        
        
        /// <summary>
        /// Where to put expanded data
        /// </summary>
        public static readonly string LocalExpandedPath = Path.Combine(Path.GetTempPath(), "dump.txt");
    }
}