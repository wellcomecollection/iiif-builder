using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// The outcome of <see cref="IIdentifiersApi.Resolve"/>: every candidate that was tried, in order.
    /// </summary>
    public class IdentifierResolution
    {
        public IdentifierResolution(string identifier)
        {
            Identifier = identifier;
        }

        /// <summary>
        /// The string as supplied.
        /// </summary>
        public string Identifier { get; }

        public List<CandidateLookup> Lookups { get; } = new List<CandidateLookup>();

        public IEnumerable<CandidateLookup> Found => Lookups.Where(l => l.Found);

        public bool AnyFound => Lookups.Any(l => l.Found);

        /// <summary>
        /// The lookups whose identifier set carries a mets row (see <see cref="IdentifierSet.PreservationIdentifier"/>),
        /// one per distinct preservation identifier value. This is usually the thing we most want to know.
        /// </summary>
        public IEnumerable<CandidateLookup> PreservationIdentifierLookups => Lookups
            .Where(l => l.IdentifierSet?.PreservationIdentifier?.Value != null)
            .GroupBy(l => l.IdentifierSet!.PreservationIdentifier!.Value!.ToLowerInvariant())
            .Select(g => g.First());
    }

    public class CandidateLookup
    {
        public CandidateLookup(IdentifierCandidate candidate, string apiUrl)
        {
            Candidate = candidate;
            ApiUrl = apiUrl;
        }

        public IdentifierCandidate Candidate { get; }
        public string ApiUrl { get; }

        /// <summary>
        /// Null if the API returned 404, or if the request failed (see Error).
        /// </summary>
        public IdentifierSet? IdentifierSet { get; set; }

        /// <summary>
        /// Set if the request failed with something other than a 404.
        /// </summary>
        public string? Error { get; set; }

        public bool Found => IdentifierSet != null;
    }
}
