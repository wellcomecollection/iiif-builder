using System.Threading.Tasks;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Client for the Wellcome Collection Identifiers API (RFC 089): a read-only lookup of
    /// canonical catalogue identifiers to their source identifiers, and back.
    /// https://github.com/wellcomecollection/docs/tree/main/rfcs/089-identifiers-api
    /// </summary>
    public interface IIdentifiersApi
    {
        /// <summary>
        /// Forward lookup: GET /identifiers/v1/{canonicalId}
        /// </summary>
        /// <param name="canonicalId">An 8 character catalogue identifier, e.g., vsyg427x</param>
        /// <returns>The identifier set, or null if the API returned 404</returns>
        /// <exception cref="IdentifiersApiException">For any other non-success response, e.g., a 400 for a malformed id</exception>
        Task<IdentifierSet?> GetByCanonicalId(string canonicalId);

        /// <summary>
        /// Bare reverse lookup: GET /identifiers/v1/by-source/{sourceSystem}/{value}?type={type}
        /// </summary>
        /// <param name="sourceSystem">e.g., sierra-system-number, calm-record-id, mets</param>
        /// <param name="value">The identifier in that source system, e.g., b14658197</param>
        /// <param name="type">Work, Image or Item (defaults to Work)</param>
        /// <returns>The canonical id, or null if the API returned 404</returns>
        Task<CanonicalIdRef?> GetBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work);

        /// <summary>
        /// Reverse lookup with siblings: GET /identifiers/v1/by-source/{sourceSystem}/{value}?type={type}&amp;include=siblings
        /// Returns the same shape as the forward lookup.
        /// </summary>
        /// <returns>The identifier set, or null if the API returned 404</returns>
        Task<IdentifierSet?> GetSiblingsBySourceIdentifier(string sourceSystem, string value, string type = OntologyType.Work);

        /// <summary>
        /// The API has no bare-value lookup (RFC 089 open question 4), so this generates candidate
        /// (sourceSystem, value, type) tuples from the shape of the supplied string and looks each one up.
        /// Every attempt is recorded, whether or not it found anything.
        /// </summary>
        Task<IdentifierResolution> Resolve(string identifier);

        /// <summary>
        /// The URL that <see cref="GetByCanonicalId"/> would request.
        /// </summary>
        string GetApiUrl(string canonicalId);

        /// <summary>
        /// The URL that the reverse lookups would request.
        /// </summary>
        string GetApiUrl(string sourceSystem, string value, string type = OntologyType.Work, bool includeSiblings = false);
    }
}
