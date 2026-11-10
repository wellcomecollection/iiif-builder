using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Composes the Identifiers API (the catalogue's ID registry), the catalogue API and the storage
    /// service to answer questions none of them can answer alone. See docs/identifiers-api-and-dds-identity.md,
    /// "API helpers that navigate both services".
    /// </summary>
    public interface IIdentifierNavigator
    {
        /// <summary>
        /// Everything the dashboard resolve page wants to know about an arbitrary identifier string:
        /// the DDS's own identity, the registry's answer, the public work by both routes, the preservation
        /// identifier(s) with recovered casing, and whether the preserved files can be found.
        /// </summary>
        Task<IdentifierNavigation> Navigate(string identifier);

        /// <summary>
        /// Fetch a work by a registry canonical id, following the catalogue API's redirect from a
        /// non-public canonical id (e.g. a METS work's) to the public work it merged into.
        /// </summary>
        Task<PublicWorkMatch> GetPublicWork(string canonicalId, string? description = null);

        /// <summary>
        /// The preservation identifiers of the storage objects merged into a public work, read from its
        /// items' iiif-presentation locations (reliable) and its mets identifiers (present on some works only).
        /// </summary>
        IReadOnlyList<string> GetPreservationIdentifiersForWork(Work work);

        /// <summary>
        /// RFC 091, "Finding the predecessor": the sierra-system-number sibling of a folio-instance id is the
        /// legacy b number, which is the preservation identifier. Null when there is no such sibling.
        /// </summary>
        Task<string?> FindPredecessorPreservationIdentifier(string folioInstanceId);

        /// <summary>
        /// The public work for an identifier, by the catalogue's identifiers= query (with the selection rule
        /// for several results) cross-checked against the registry's mets row and the catalogue redirect.
        /// The query is the only route for identifiers the registry never mints, such as a CALM AltRefNo;
        /// the registry is the merger's own statement, so it wins when both answer and they disagree.
        /// </summary>
        Task<WorkLookup> LookupWorkByAnyIdentifier(string identifier);

        /// <summary>
        /// <see cref="LookupWorkByAnyIdentifier"/>, reduced to the work. Null when neither route answers or
        /// the query is ambiguous and the registry has no row.
        /// </summary>
        Task<Work?> FindWorkByAnyIdentifier(string identifier);

        /// <summary>
        /// Probe the storage service for a preservation identifier, one space at a time, stopping at the
        /// first hit. The identifier must have the casing the storage service holds.
        /// </summary>
        Task<IReadOnlyList<StorageProbeResult>> LocatePreservedFiles(string preservationIdentifier);
    }
}
