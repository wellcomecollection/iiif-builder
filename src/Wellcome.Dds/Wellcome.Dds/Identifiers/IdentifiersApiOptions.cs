namespace Wellcome.Dds.Identifiers
{
    public class IdentifiersApiOptions
    {
        /// <summary>
        /// The versioned root of the API, with no trailing slash.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.wellcomecollection.org/identifiers/v1";

        /// <summary>
        /// Sent as the x-api-key header; validated by API Gateway. Identifies this consumer.
        /// </summary>
        public string? ApiKey { get; set; }
    }
}
