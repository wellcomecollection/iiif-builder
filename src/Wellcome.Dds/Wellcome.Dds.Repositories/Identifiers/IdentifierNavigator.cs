using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Repositories.Identifiers
{
    /// <summary>
    /// See <see cref="IIdentifierNavigator"/>. Keeps the three clients independent of each other: this is
    /// the only place that knows how their answers fit together.
    /// </summary>
    public class IdentifierNavigator : IIdentifierNavigator
    {
        private const string IiifPresentationLocationType = "iiif-presentation";

        /// <summary>
        /// Identifier forms that are lowercase by nature, so the registry's lowercased value is already usable.
        /// </summary>
        private static readonly Regex LowercaseByNature = new Regex("^(b[0-9]{7}[0-9x]|in[0-9]{5,})$");

        private readonly IIdentifiersApi identifiersApi;
        private readonly ICatalogue catalogue;
        private readonly IIdentityService identityService;
        private readonly IStorageServiceProbe storageProbe;
        private readonly ILogger<IdentifierNavigator> logger;

        public IdentifierNavigator(
            IIdentifiersApi identifiersApi,
            ICatalogue catalogue,
            IIdentityService identityService,
            IStorageServiceProbe storageProbe,
            ILogger<IdentifierNavigator> logger)
        {
            this.identifiersApi = identifiersApi;
            this.catalogue = catalogue;
            this.identityService = identityService;
            this.storageProbe = storageProbe;
            this.logger = logger;
        }

        public async Task<IdentifierNavigation> Navigate(string identifier)
        {
            var nav = new IdentifierNavigation(identifier.Trim());

            // 1. The DDS's own view. A read request: never creates a record.
            try
            {
                nav.DdsIdentity = identityService.GetIdentity(nav.Identifier);
            }
            catch (FormatException ex)
            {
                nav.DdsIdentityError = ex.Message;
            }

            // 2. The registry.
            nav.Registry = await identifiersApi.Resolve(nav.Identifier);

            // 3. What the catalogue makes of each registry canonical id.
            foreach (var lookup in nav.Registry.Found)
            {
                nav.PublicWorks.Add(await GetPublicWork(lookup.IdentifierSet!.CanonicalId!, lookup.Candidate.ToString()));
            }

            // 4. The catalogue's own identifiers query, independent of the registry (the page shows both routes
            //    side by side, so the cross-check in LookupWorkByAnyIdentifier is not repeated here).
            try
            {
                nav.WorkByIdentifiersQuerySelection = await catalogue.FindWorksByOtherIdentifier(nav.Identifier);
                nav.WorkByIdentifiersQuery = nav.WorkByIdentifiersQuerySelection.Chosen;
                if (nav.WorkByIdentifiersQuery != null)
                {
                    nav.PreservationIdentifiersFromWork.AddRange(GetPreservationIdentifiersForWork(nav.WorkByIdentifiersQuery));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Catalogue identifiers query failed for {identifier}", nav.Identifier.LogSafe());
                nav.WorkByIdentifiersQueryError = ex.Message;
            }

            // 5. Preservation identifiers from the registry's mets rows, with casing recovered where possible.
            foreach (var lookup in nav.Registry.PreservationIdentifierLookups)
            {
                var set = lookup.IdentifierSet!;
                var match = new PreservationIdentifierMatch(set.PreservationIdentifier!.Value!, set.CanonicalId!);
                RecoverCasing(match, nav);
                nav.PreservationIdentifiers.Add(match);
            }

            // 6. Probe storage for every distinct identifier that could be a preservation identifier.
            foreach (var probeValue in GetProbeValues(nav))
            {
                nav.StorageProbes.AddRange(await LocatePreservedFiles(probeValue));
            }
            nav.StorageApi = storageProbe.StorageApi;

            return nav;
        }

        public async Task<PublicWorkMatch> GetPublicWork(string canonicalId, string? description = null)
        {
            var match = new PublicWorkMatch(canonicalId, description);
            try
            {
                // The catalogue client's HttpClient follows the 302 from a non-public canonical id, so the
                // returned work's Id is the public work; comparing it with the requested id reveals the redirect.
                match.Work = await catalogue.GetWorkByWorkId(canonicalId);
                if (match.Work?.Id == null)
                {
                    match.Error = "No work returned";
                }
            }
            catch (HttpRequestException ex)
            {
                match.StatusCode = (int?) ex.StatusCode;
                match.Error = ex.StatusCode == HttpStatusCode.NotFound
                    ? "Not a work: the catalogue API has nothing at this canonical id"
                    : ex.Message;
            }
            return match;
        }

        public IReadOnlyList<string> GetPreservationIdentifiersForWork(Work work)
        {
            var fromLocations = work.GetIiifPresentationIdentifiers();

            // mets identifiers corroborate but are absent from some merged works, so they come second.
            var fromIdentifiers = work.Identifiers.AnyItems()
                .Where(i => i.IdentifierType?.Id == SourceSystems.Mets && i.Value.HasText())
                .Select(i => i.Value!);

            return fromLocations.Concat(fromIdentifiers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<string?> FindPredecessorPreservationIdentifier(string folioInstanceId)
        {
            var set = await identifiersApi.GetSiblingsBySourceIdentifier(SourceSystems.FolioInstance, folioInstanceId);
            return set?.SourceIdentifiers?
                .FirstOrDefault(si => si.SourceSystem == SourceSystems.SierraSystemNumber)?
                .Value;
        }

        public async Task<Work?> FindWorkByAnyIdentifier(string identifier)
        {
            return (await LookupWorkByAnyIdentifier(identifier)).Work;
        }

        public async Task<WorkLookup> LookupWorkByAnyIdentifier(string identifier)
        {
            var lookup = new WorkLookup(identifier);

            // Steps 1, 2 and 4: the query, ranked, or null when ambiguous.
            try
            {
                lookup.Selection = await catalogue.FindWorksByOtherIdentifier(identifier);
            }
            catch (HttpRequestException ex)
            {
                lookup.SelectionError = ex.Message;
                logger.LogWarning(ex, "Identifiers query failed for {Identifier}", identifier.LogSafe());
            }
            var bySearch = lookup.Selection?.Chosen;

            // Step 3: the registry's mets row, then the catalogue redirect, independent of search ranking.
            try
            {
                var set = await identifiersApi.GetSiblingsBySourceIdentifier(SourceSystems.Mets, identifier);
                if (set?.CanonicalId != null)
                {
                    lookup.RegistryCanonicalId = set.CanonicalId;
                    lookup.ViaRegistry = await GetPublicWork(set.CanonicalId, $"{SourceSystems.Mets}/{identifier}");
                }
            }
            catch (IdentifiersApiException ex)
            {
                lookup.RegistryError = ex.Message;
                logger.LogWarning(ex, "Registry lookup failed for {Identifier}; the identifiers query stands alone", identifier.LogSafe());
            }
            var byRegistry = lookup.ViaRegistry is { Found: true } ? lookup.ViaRegistry.Work : null;

            if (byRegistry == null)
            {
                lookup.Work = bySearch;
                lookup.Route = bySearch == null ? WorkLookupRoute.None : WorkLookupRoute.Search;
                lookup.Note = (lookup.RegistryCanonicalId == null
                                  ? "The registry has no mets row for this identifier (not a preservation identifier, or pipeline lag); "
                                  : $"The registry's canonical id {lookup.RegistryCanonicalId} is not a work in the catalogue; ")
                              + (bySearch == null ? "the identifiers query found nothing usable" : "the identifiers query stands alone")
                              + (lookup.Selection == null ? "" : $" ({lookup.Selection.Reason})");
            }
            else if (bySearch == null)
            {
                lookup.Work = byRegistry;
                lookup.Route = WorkLookupRoute.Registry;
                lookup.Note = $"The identifiers query found nothing usable ({lookup.Selection?.Reason ?? lookup.SelectionError}); " +
                              $"the registry's mets row {lookup.RegistryCanonicalId} redirects to {byRegistry.Id}";
            }
            else if (bySearch.Id == byRegistry.Id)
            {
                lookup.Work = bySearch;
                lookup.Route = WorkLookupRoute.Both;
                lookup.Note = $"Both routes reach {bySearch.Id} ({lookup.Selection!.Reason})";
            }
            else
            {
                // The redirect is the merger's own statement of which work the digital object belongs to.
                lookup.Work = byRegistry;
                lookup.Route = WorkLookupRoute.RegistryOverSearch;
                lookup.Note = $"ROUTES DISAGREE: the identifiers query chose {bySearch.Id} ({lookup.Selection!.Reason}) " +
                              $"but the registry's mets row {lookup.RegistryCanonicalId} redirects to {byRegistry.Id}; preferring the registry";
                logger.LogWarning("Work lookup for {Identifier}: {Note}", identifier.LogSafe(), lookup.Note);
            }
            return lookup;
        }

        public async Task<IReadOnlyList<StorageProbeResult>> LocatePreservedFiles(string preservationIdentifier)
        {
            var results = new List<StorageProbeResult>();
            foreach (var storageSpace in StorageSpace.All)
            {
                var result = await storageProbe.Probe(storageSpace!, preservationIdentifier);
                results.Add(result);
                if (result.Found)
                {
                    // A bag lives in exactly one space, and the storage client caches by identifier alone,
                    // so a second probe after a hit would return the first bag again.
                    break;
                }
            }
            return results;
        }

        private void RecoverCasing(PreservationIdentifierMatch match, IdentifierNavigation nav)
        {
            if (LowercaseByNature.IsMatch(match.Value))
            {
                match.CasedValue = match.Value;
                match.CasingSource = "lowercase by nature";
                return;
            }

            // The DDS identities table is keyed on the lowercased value and holds the generator's casing.
            try
            {
                var identity = identityService.GetIdentity(match.Value);
                if (identity.FromGenerator)
                {
                    match.CasedValue = identity.PackageIdentifier;
                    match.CasingSource = "DDS identity record";
                    return;
                }
            }
            catch (FormatException)
            {
                // not something the DDS can parse; fall through
            }

            // The catalogue work's calm-ref-no keeps its case.
            var works = nav.PublicWorks.Where(p => p.Found).Select(p => p.Work!);
            if (nav.WorkByIdentifiersQuery != null)
            {
                works = works.Append(nav.WorkByIdentifiersQuery);
            }
            var calmRef = works
                .SelectMany(w => w.Identifiers.AnyItems())
                .Where(i => i.IdentifierType?.Id == SourceSystems.CalmRefNo)
                .Select(i => i.Value)
                .FirstOrDefault(v => string.Equals(v, match.Value, StringComparison.OrdinalIgnoreCase));
            if (calmRef != null)
            {
                match.CasedValue = calmRef;
                match.CasingSource = "catalogue calm-ref-no";
                return;
            }

            // The caller typed it; plausible but unverified.
            var supplied = nav.DdsIdentity?.PackageIdentifier ?? nav.Identifier;
            if (string.Equals(supplied, match.Value, StringComparison.OrdinalIgnoreCase) && supplied != match.Value)
            {
                match.CasedValue = supplied;
                match.CasingSource = "as supplied (unverified)";
            }
        }

        private static IEnumerable<string> GetProbeValues(IdentifierNavigation nav)
        {
            var values = new List<string>();

            // The supplied string, if its shape says it is itself a preservation identifier (the candidate
            // generator produces a mets candidate for b numbers and CALM-ref-shaped strings).
            var metsCandidate = IdentifierCandidates.GetCandidates(nav.Identifier)
                .FirstOrDefault(c => c.SourceSystem == SourceSystems.Mets);
            if (metsCandidate != null)
            {
                values.Add(metsCandidate.Value);
            }

            values.AddRange(nav.PreservationIdentifiers.Select(p => p.ProbeValue));
            values.AddRange(nav.PreservationIdentifiersFromWork);

            // Ordinal: a differently cased duplicate is a different storage key, and showing both is the point.
            return values.Where(v => v.HasText()).Distinct(StringComparer.Ordinal);
        }
    }
}
