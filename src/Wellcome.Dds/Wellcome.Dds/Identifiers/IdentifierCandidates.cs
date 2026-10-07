using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Shape-based candidate generation. The Identifiers API only answers qualified queries
    /// (sourceSystem + value + type), so the DDS has to guess which tuples a bare string might be
    /// (RFC 089 open question 4). This is deliberately generous: a dashboard user would rather see
    /// three 404s than miss the one that hits.
    /// </summary>
    public static class IdentifierCandidates
    {
        /// <summary>
        /// e.g., V0012345, L0012345EB, M0001234
        /// </summary>
        private static readonly Regex MiroImageNumber = new Regex("^[A-Z][0-9]{7}[A-Z]{0,3}$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Broader than IsBNumber(): also accepts the 'a' placeholder check digit (b1465819a) that Sierra allows.
        /// </summary>
        private static readonly Regex BNumberLike = new Regex("^b[0-9]{7}[0-9xa]?$", RegexOptions.IgnoreCase);

        /// <summary>
        /// FOLIO instance HRID, e.g., in00012345 (the default FOLIO prefix "in" plus a zero-padded sequence).
        /// </summary>
        private static readonly Regex FolioInstanceHrid = new Regex("^in[0-9]{5,}$", RegexOptions.IgnoreCase);

        public static List<IdentifierCandidate> GetCandidates(string? identifier)
        {
            var candidates = new List<IdentifierCandidate>();
            var s = identifier?.Trim();
            if (string.IsNullOrEmpty(s))
            {
                return candidates;
            }

            if (CanonicalIdentifier.IsValid(s))
            {
                candidates.Add(new IdentifierCandidate(s, null, OntologyType.Work,
                    "matches the canonical id pattern"));
            }

            if (s.IsBNumber() || BNumberLike.IsMatch(s))
            {
                var bNumber = WellcomeLibraryIdentifiers.GetNormalisedBNumber(s, false) ?? s;
                const string reason = "looks like a b number";
                // mets first: for a digitised object the b number is the METS record identifier and the
                // preservation identifier, so the mets row is the one we most want to see
                candidates.Add(new IdentifierCandidate(bNumber, SourceSystems.Mets, OntologyType.Work,
                    reason + "; a digitised b number is minted under mets as the preservation identifier"));
                candidates.Add(new IdentifierCandidate(bNumber, SourceSystems.SierraSystemNumber, OntologyType.Work, reason));
                if (bNumber.Length == 9)
                {
                    candidates.Add(new IdentifierCandidate(bNumber.Substring(1, 7), SourceSystems.SierraIdentifier,
                        OntologyType.Work, reason + "; 7 digit form without prefix or check digit"));
                }
                return candidates;
            }

            if (Guid.TryParse(s, out var guid))
            {
                var normalised = guid.ToString("D");
                const string reason = "is a GUID";
                candidates.Add(new IdentifierCandidate(normalised, SourceSystems.CalmRecordId, OntologyType.Work, reason));
                candidates.Add(new IdentifierCandidate(normalised, SourceSystems.AxiellGuid, OntologyType.Work, reason));
                candidates.Add(new IdentifierCandidate(normalised, SourceSystems.FolioInstance, OntologyType.Work, reason));
                return candidates;
            }

            if (FolioInstanceHrid.IsMatch(s))
            {
                candidates.Add(new IdentifierCandidate(s, SourceSystems.FolioInstanceHrid, OntologyType.Work,
                    "looks like a FOLIO instance HRID"));
                return candidates;
            }

            if (MiroImageNumber.IsMatch(s))
            {
                const string reason = "looks like a Miro image number";
                candidates.Add(new IdentifierCandidate(s, SourceSystems.MiroImageNumber, OntologyType.Work, reason));
                candidates.Add(new IdentifierCandidate(s, SourceSystems.MiroImageNumber, OntologyType.Image, reason));
                return candidates;
            }

            if (candidates.Count == 0)
            {
                // Could be anything; CALM references are the only remaining shape the DDS knows about.
                // A born-digital object's preservation identifier is its CALM ref (lowercased, slashes kept),
                // and like a digitised b number it is minted under mets with its own canonical id,
                // e.g. ARTCOO/B/15 -> mets: artcoo/b/15 and calm-ref-no: ARTCOO/B/15. The lookup is case-insensitive.
                var calmRef = s.Replace('_', '/');
                const string reason = "no other shape matched; try as a CALM reference";
                candidates.Add(new IdentifierCandidate(calmRef, SourceSystems.Mets, OntologyType.Work,
                    reason + "; a born-digital preservation identifier is also minted under mets"));
                candidates.Add(new IdentifierCandidate(calmRef, SourceSystems.CalmRefNo, OntologyType.Work, reason));
                candidates.Add(new IdentifierCandidate(calmRef, SourceSystems.CalmAltRefNo, OntologyType.Work, reason));
            }

            return candidates;
        }
    }
}
