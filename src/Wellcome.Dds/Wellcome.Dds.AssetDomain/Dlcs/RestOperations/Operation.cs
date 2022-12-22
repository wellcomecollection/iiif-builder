using System;

namespace Wellcome.Dds.AssetDomain.Dlcs.RestOperations
{
    public class Operation<TRequest, TResponse>
    {
        public Operation(Uri uri, string httpMethod)
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
        public string HttpMethod { get; set; }

    }
}
