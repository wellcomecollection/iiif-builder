using System;
using System.Net;
using System.Net.Http;

namespace Wellcome.Dds.AssetDomain.Dlcs.RestOperations
{
    public class Operation<TRequest, TResponse>
    {
        public Operation(Uri uri, HttpMethod httpMethod)
        {
            Uri = uri;
            HttpMethod = httpMethod;
        }

        public Uri Uri { get; set; }
        public TRequest? RequestObject { get; set; }
        public TResponse? ResponseObject { get; set; }
        public string? RequestJson { get; set; }
        public string? ResponseJson { get; set; }
        public Error? Error { get; set; }
        public HttpMethod HttpMethod { get; set; }
        public HttpStatusCode ResponseStatus { get; set; }
    }
}
