using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// The result of <see cref="IIdentifierNavigator.Navigate"/>: every route from an identifier string,
    /// kept separate so that disagreements between them are visible.
    /// </summary>
    public class IdentifierNavigation
    {
        public IdentifierNavigation(string identifier)
        {
            Identifier = identifier;
        }

        /// <summary>
        /// The string as supplied (trimmed).
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// The DDS's own view, from the identity service: parsed, or the stored record if there is one.
        /// Null if the string could not be parsed at all.
        /// </summary>
        public DdsIdentity? DdsIdentity { get; set; }
        public string? DdsIdentityError { get; set; }

        /// <summary>
        /// The registry's answer: every candidate tuple tried and what came back.
        /// </summary>
        public IdentifierResolution? Registry { get; set; }

        /// <summary>
        /// For each registry hit, the catalogue API's view of that canonical id: the public work it
        /// redirects to, the work itself, or a 404 for a canonical id that is not a work.
        /// </summary>
        public List<PublicWorkMatch> PublicWorks { get; } = new List<PublicWorkMatch>();

        /// <summary>
        /// The public work reached by the catalogue's identifiers= query, independent of the registry.
        /// </summary>
        public Work? WorkByIdentifiersQuery { get; set; }
        public string? WorkByIdentifiersQueryError { get; set; }

        /// <summary>
        /// Preservation identifiers read from <see cref="WorkByIdentifiersQuery"/>'s IIIF locations and mets identifiers.
        /// </summary>
        public List<string> PreservationIdentifiersFromWork { get; } = new List<string>();

        /// <summary>
        /// Preservation identifiers from the registry's mets rows, with casing recovered where possible.
        /// </summary>
        public List<PreservationIdentifierMatch> PreservationIdentifiers { get; } = new List<PreservationIdentifierMatch>();

        /// <summary>
        /// The storage service probes made, in order, with their outcomes.
        /// </summary>
        public List<StorageProbeResult> StorageProbes { get; } = new List<StorageProbeResult>();

        /// <summary>
        /// Which storage service was probed, so a miss on staging is not misread.
        /// </summary>
        public string? StorageApi { get; set; }

        /// <summary>
        /// Distinct public work ids reached through the registry route.
        /// </summary>
        public IEnumerable<string> PublicWorkIdsViaRegistry => PublicWorks
            .Where(p => p.Found)
            .Select(p => p.Work!.Id!)
            .Distinct();

        /// <summary>
        /// True if the registry route and the identifiers query reached the same public work; false if they
        /// reached different ones; null if one of the routes found nothing.
        /// </summary>
        public bool? RoutesAgree
        {
            get
            {
                var viaRegistry = PublicWorkIdsViaRegistry.ToList();
                var viaQuery = WorkByIdentifiersQuery?.Id;
                if (viaRegistry.Count == 0 || viaQuery == null)
                {
                    return null;
                }
                return viaRegistry.All(id => id == viaQuery);
            }
        }

        public IEnumerable<StorageProbeResult> PreservedFilesFound => StorageProbes.Where(p => p.Found);
    }

    /// <summary>
    /// A registry mets row, which is the preservation identifier lowercased, plus the storage-usable casing
    /// if it could be recovered. See the design doc on why the registry's value is not a storage key.
    /// </summary>
    public class PreservationIdentifierMatch
    {
        public PreservationIdentifierMatch(string value, string metsCanonicalId)
        {
            Value = value;
            MetsCanonicalId = metsCanonicalId;
        }

        /// <summary>
        /// As the registry returned it (lowercased by the METS transformer).
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The METS work's own canonical id; never the public work's.
        /// </summary>
        public string MetsCanonicalId { get; }

        /// <summary>
        /// The form the storage service will accept, if known.
        /// </summary>
        public string? CasedValue { get; set; }

        /// <summary>
        /// Where the casing came from, for display: e.g. "DDS identity record", "catalogue calm-ref-no",
        /// "lowercase by nature". Null means unknown.
        /// </summary>
        public string? CasingSource { get; set; }

        public string ProbeValue => CasedValue ?? Value;
    }

    /// <summary>
    /// What the catalogue API says about a registry canonical id.
    /// </summary>
    public class PublicWorkMatch
    {
        public PublicWorkMatch(string registryCanonicalId, string? description)
        {
            RegistryCanonicalId = registryCanonicalId;
            Description = description;
        }

        public string RegistryCanonicalId { get; }

        /// <summary>
        /// Which registry row this canonical id came from, for display.
        /// </summary>
        public string? Description { get; }

        public Work? Work { get; set; }
        public int? StatusCode { get; set; }
        public string? Error { get; set; }

        public bool Found => Work?.Id != null;

        /// <summary>
        /// True when the catalogue API redirected: the registry canonical id is not the public work's.
        /// </summary>
        public bool Redirected => Found && Work!.Id != RegistryCanonicalId;
    }
}
