using Newtonsoft.Json.Linq;
using System.Net;

namespace OAuth2
{
    public class PostBodyResponse
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public JObject ResponseObject { get; set; }
        public string TransportError { get; set; }
    }
}
