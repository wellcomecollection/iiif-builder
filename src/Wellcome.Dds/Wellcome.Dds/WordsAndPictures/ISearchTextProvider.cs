
using System.Threading.Tasks;

namespace Wellcome.Dds.WordsAndPictures
{
    public interface ISearchTextProvider
    {
        // TODO - rename this interface, it's not just for search

        // No longer used - now we want IDENTIFIERS!
        // Task<Text> GetSearchText(string bNumber, int manifestation);
        Task<Text> GetSearchText(string identifier); // e.g., b12345678_0003
    }
}
