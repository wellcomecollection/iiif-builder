
using System.Threading.Tasks;

namespace Wellcome.Dds.WordsAndPictures
{
    public interface ISearchTextProvider
    {
        // TODO - rename this interface, it's not just for search
        
        Task<Text?> GetSearchText(string identifier); // e.g., b12345678_0003
    }
}
