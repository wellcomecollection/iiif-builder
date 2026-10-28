namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// The body of a 4xx or 5xx response.
    /// </summary>
    public class IdentifiersApiError
    {
        /// <summary>
        /// Stable machine-readable code, e.g., notFound, badRequest, internalServerError
        /// </summary>
        public string? Error { get; set; }
        public string? Message { get; set; }
    }
}
