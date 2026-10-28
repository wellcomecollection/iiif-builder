using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Which route produced the work in a <see cref="WorkLookup"/>.
    /// </summary>
    public enum WorkLookupRoute
    {
        /// <summary>Neither route produced a work.</summary>
        None,
        /// <summary>The catalogue's identifiers= query alone; the registry had no row (or could not be asked).</summary>
        Search,
        /// <summary>The registry's mets row and the catalogue redirect; the query found nothing or was ambiguous.</summary>
        Registry,
        /// <summary>Both routes reached the same work.</summary>
        Both,
        /// <summary>The routes disagreed and the registry's answer was preferred; logged as a warning.</summary>
        RegistryOverSearch
    }

    /// <summary>
    /// The full answer to "which public work is this identifier's?", by the two independent routes and
    /// the rule for combining them (design doc, "Choosing the work when the identifiers query returns
    /// several", step 3).
    /// </summary>
    public class WorkLookup
    {
        public WorkLookup(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        /// <summary>
        /// The catalogue's identifiers= query and the ranking rule applied to its results (steps 1, 2 and 4).
        /// </summary>
        public WorkSelection? Selection { get; set; }
        public string? SelectionError { get; set; }

        /// <summary>
        /// The registry's canonical id for the identifier as a mets (preservation) identifier, if it has one.
        /// </summary>
        public string? RegistryCanonicalId { get; set; }

        /// <summary>
        /// What the catalogue API makes of that canonical id: the public work, by redirect.
        /// </summary>
        public PublicWorkMatch? ViaRegistry { get; set; }
        public string? RegistryError { get; set; }

        public Work? Work { get; set; }
        public WorkLookupRoute Route { get; set; } = WorkLookupRoute.None;

        /// <summary>
        /// A sentence saying how the answer was arrived at, for logs and the dashboard.
        /// </summary>
        public string Note { get; set; } = "";

        public bool RoutesDisagreed => Route == WorkLookupRoute.RegistryOverSearch;
    }
}
