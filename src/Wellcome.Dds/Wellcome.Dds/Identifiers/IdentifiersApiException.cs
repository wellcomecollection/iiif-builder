using System;
using System.Net;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Thrown for a non-success response other than 404 (which the client surfaces as null).
    /// </summary>
    public class IdentifiersApiException : Exception
    {
        public string Url { get; }
        public HttpStatusCode StatusCode { get; }
        public IdentifiersApiError? Error { get; }

        public IdentifiersApiException(string url, HttpStatusCode statusCode, IdentifiersApiError? error)
            : base(BuildMessage(url, statusCode, error))
        {
            Url = url;
            StatusCode = statusCode;
            Error = error;
        }

        private static string BuildMessage(string url, HttpStatusCode statusCode, IdentifiersApiError? error)
        {
            var detail = error?.Message ?? error?.Error;
            return detail == null
                ? $"Identifiers API returned {(int) statusCode} {statusCode} for '{url}'"
                : $"Identifiers API returned {(int) statusCode} {statusCode} for '{url}': {detail}";
        }
    }
}
