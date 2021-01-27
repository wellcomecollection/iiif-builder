using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wellcome.Dds.Catalogue
{
    public interface ICatalogue
    {
        /// <summary>
        /// Find a work in the catalogue
        /// </summary>
        /// <param name="identifier">Usually a B Number, but could be a CALM-ref</param>
        /// <returns>A Catalogue Work</returns>
        Task<Work> GetWorkByOtherIdentifier(string identifier);
        
        /// <summary>
        /// Get a work in the catalogue
        /// </summary>
        /// <param name="workId">Must be the Catalogue API ID, not a b number or other identifier.</param>
        /// <returns>A Catalogue Work</returns>
        Task<Work> GetWorkByWorkId(string workId);
        
        Task<WorkResultPage> GetWorkResultPage(string query, string identifiers);
        
        Task<WorkResultPage> GetWorkResultPage(string query, string identifiers, IEnumerable<string> include, int pageSize);

        string GetCatalogueApiUrl(string workId, string[] include = null);
        
        /// <summary>
        /// Construct a catalogue record from a line in a dump file rather than via the API 
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        Work FromDumpLine(string line, JsonSerializerOptions options);
    }
}
