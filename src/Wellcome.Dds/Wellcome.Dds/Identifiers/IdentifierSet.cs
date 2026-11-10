using System.Linq;
using System.Text.Json.Serialization;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// A canonical id and the source identifiers sharing it. Returned by the forward lookup and by
    /// the reverse lookup with include=siblings.
    /// </summary>
    public class IdentifierSet
    {
        public string? CanonicalId { get; set; }

        /// <summary>
        /// Ontology type of the canonical id, taken from the original (isAlias=false) row.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Ordered by createdAt descending; most recent first. Always includes the original.
        /// </summary>
        public SourceIdentifier[]? SourceIdentifiers { get; set; }

        /// <summary>
        /// The weak validator the API sent with this set, if any. Not part of the JSON body.
        /// </summary>
        [JsonIgnore]
        public string? ETag { get; set; }

        /// <summary>
        /// The Cache-Control header the API sent with this set, if any. Not part of the JSON body.
        /// </summary>
        [JsonIgnore]
        public string? CacheControl { get; set; }

        /// <summary>
        /// The single isAlias=false row.
        /// </summary>
        [JsonIgnore]
        public SourceIdentifier? Original => SourceIdentifiers?.FirstOrDefault(si => !si.IsAlias);

        /// <summary>
        /// The mets row, if this set has one. The catalogue pipeline mints the METS-sourced work's own
        /// identity as mets/{METS record identifier}, lowercased (RFC 091, "How components are keyed").
        /// Today the METS record identifier is the same string as the preservation identifier (RFC 091):
        /// the storage service externalIdentifier Goobi or Archivematica hand over, our package identifier,
        /// and the identity in our canonical IIIF Manifest and Collection URIs (RFC 085). It stays the
        /// same string under the recommended Folio HRID scheme; RFC 091 open question 2 is the case where
        /// the two could diverge. So, for now, this row is the preservation identifier, lowercased
        /// (artcoo/b/15 for ARTCOO/B/15) by the METS transformer. The API's lookup is case-insensitive,
        /// but the storage service is NOT: this value cannot be used as a bag identifier without first
        /// recovering its casing (e.g. from the DDS identities table, which is keyed on the lowercased value).
        ///
        /// The METS work keeps its own canonical id, distinct from the public work's (RFC 091, "METS works
        /// keep their own id"); the catalogue API redirects that canonical id to the public work, which is
        /// merge-derived knowledge the registry does not hold.
        /// </summary>
        [JsonIgnore]
        public SourceIdentifier? PreservationIdentifier =>
            SourceIdentifiers?.FirstOrDefault(si => si.SourceSystem == SourceSystems.Mets);
    }
}
