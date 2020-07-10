using System.Diagnostics;
using System.Net.Http.Headers;

namespace Utils
{
    public class HttpClientDebugHelpers
    {
        public static void DebugHeaders(HttpHeaders headers)
        {
            Debug.WriteLine("===== start ====================");
            foreach (var (key, value) in headers)
            {
                Debug.WriteLine($"{key}: {string.Join(",", value)}");
            }
            Debug.WriteLine("===== end   ====================");
        }
    }
}

