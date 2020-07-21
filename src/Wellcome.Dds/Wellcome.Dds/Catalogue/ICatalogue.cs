using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.Catalogue
{
    public interface ICatalogue
    {
        Task<Work> GetWork(string identifier);
        Task<WorkResultPage> GetWorkResultPage(string query, string identifiers);
        Task<WorkResultPage> GetWorkResultPage(string query, string identifiers, IEnumerable<string> include, int pageSize);
    }
}
