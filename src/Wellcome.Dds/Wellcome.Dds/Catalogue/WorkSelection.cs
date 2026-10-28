using System;
using System.Collections.Generic;
using System.Linq;
using Utils;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Catalogue
{
    /// <summary>
    /// How strongly a work returned by the catalogue's <c>identifiers=</c> query is bound to the
    /// identifier that was asked for. The query matches any identifier type, so a work can come back
    /// through an incidental one; this is the order of strength from the design doc,
    /// "Choosing the work when the identifiers query returns several".
    /// </summary>
    public enum WorkMatchStrength
    {
        /// <summary>
        /// The work's iiif-presentation location names the identifier: the merge outcome saying
        /// "this digital object belongs to this work", which is what CatalogueId records.
        /// </summary>
        PresentationLocation = 0,
        /// <summary>The identifier is the work's <c>mets</c> identifier (compared ignoring case).</summary>
        Mets = 1,
        /// <summary>The work's <c>sierra-system-number</c>, or its <c>sierra-identifier</c> as the short form of the b number.</summary>
        SierraSystemNumber = 2,
        CalmRefNo = 3,
        CalmAltRefNo = 4,
        /// <summary>Some other identifier type carries the value.</summary>
        OtherIdentifier = 5,
        /// <summary>The query matched but nothing visible on the work explains how.</summary>
        None = 6
    }

    /// <summary>
    /// The outcome of choosing one work from the <c>identifiers=</c> query's results: the work, how it
    /// was bound to the identifier, the works it beat, or nothing when the choice would be a guess.
    /// Pure: no I/O, so the rule can be tested on hand-made works.
    /// </summary>
    public class WorkSelection
    {
        public WorkSelection(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        /// <summary>
        /// The chosen work, or null when the query found nothing or the choice is ambiguous.
        /// </summary>
        public Work? Chosen { get; set; }

        public WorkMatchStrength Strength { get; set; } = WorkMatchStrength.None;

        /// <summary>
        /// Every work the query returned, strongest binding first.
        /// </summary>
        public List<Work> Candidates { get; } = new List<Work>();

        /// <summary>
        /// The candidates that were not chosen.
        /// </summary>
        public IEnumerable<Work> Others => Candidates.Where(c => !ReferenceEquals(c, Chosen));

        /// <summary>
        /// More than one work is bound as strongly as the strongest, so none was chosen.
        /// </summary>
        public bool Ambiguous { get; set; }

        /// <summary>
        /// A sentence saying why, for logs and the dashboard.
        /// </summary>
        public string Reason { get; set; } = "";

        /// <summary>
        /// Apply the rule to the works the query returned, in the order the API gave them.
        /// </summary>
        public static WorkSelection Choose(string identifier, IEnumerable<Work>? results)
        {
            var selection = new WorkSelection(identifier);
            var ranked = (results ?? Enumerable.Empty<Work>())
                .Where(w => w.Id.HasText())
                .Select(w => (Work: w, Strength: GetStrength(w, identifier)))
                .OrderBy(ws => ws.Strength)
                .ToList();
            selection.Candidates.AddRange(ranked.Select(ws => ws.Work));

            if (ranked.Count == 0)
            {
                selection.Reason = "The identifiers query returned no works";
                return selection;
            }
            if (ranked.Count == 1)
            {
                selection.Chosen = ranked[0].Work;
                selection.Strength = ranked[0].Strength;
                selection.Reason = $"The only work returned ({Describe(ranked[0].Strength)})";
                return selection;
            }

            var best = ranked[0].Strength;
            var atBest = ranked.Where(ws => ws.Strength == best).ToList();
            if (atBest.Count == 1 && best != WorkMatchStrength.None)
            {
                selection.Chosen = atBest[0].Work;
                selection.Strength = best;
                selection.Reason = $"Chosen from {ranked.Count} works: {Describe(best)}; " +
                                   $"the others matched more weakly ({string.Join(", ", ranked.Skip(1).Select(ws => $"{ws.Work.Id} {Describe(ws.Strength)}"))})";
                return selection;
            }

            selection.Ambiguous = true;
            selection.Reason = best == WorkMatchStrength.None
                ? $"{ranked.Count} works returned and none shows how it matched: {string.Join(", ", ranked.Select(ws => ws.Work.Id))}"
                : $"{atBest.Count} of {ranked.Count} works are bound equally strongly ({Describe(best)}): {string.Join(", ", atBest.Select(ws => ws.Work.Id))}";
            return selection;
        }

        /// <summary>
        /// The strongest way this work is bound to the identifier.
        /// </summary>
        public static WorkMatchStrength GetStrength(Work work, string identifier)
        {
            if (work.GetIiifPresentationIdentifiers().Any(pid => pid.Equals(identifier, StringComparison.OrdinalIgnoreCase)))
            {
                return WorkMatchStrength.PresentationLocation;
            }

            var shortBNumber = identifier.IsBNumber() ? identifier.ToShortBNumber().ToString() : null;
            var strengths = work.Identifiers.AnyItems()
                .Where(i => i.Value.HasText())
                .Select(i => StrengthOfIdentifier(i, identifier, shortBNumber))
                .Where(s => s != WorkMatchStrength.None)
                .ToList();
            return strengths.Count == 0 ? WorkMatchStrength.None : strengths.Min();
        }

        private static WorkMatchStrength StrengthOfIdentifier(Identifier workIdentifier, string identifier, string? shortBNumber)
        {
            var type = workIdentifier.IdentifierType?.Id;
            var value = workIdentifier.Value!;
            if (type == "sierra-identifier")
            {
                // Sierra's own 7-digit form of the b number
                return shortBNumber != null && value == shortBNumber ? WorkMatchStrength.SierraSystemNumber : WorkMatchStrength.None;
            }
            if (!value.Equals(identifier, StringComparison.OrdinalIgnoreCase))
            {
                return WorkMatchStrength.None;
            }
            return type switch
            {
                "mets" => WorkMatchStrength.Mets,
                "sierra-system-number" => WorkMatchStrength.SierraSystemNumber,
                "calm-ref-no" => WorkMatchStrength.CalmRefNo,
                "calm-altref-no" => WorkMatchStrength.CalmAltRefNo,
                _ => WorkMatchStrength.OtherIdentifier
            };
        }

        public static string Describe(WorkMatchStrength strength)
        {
            return strength switch
            {
                WorkMatchStrength.PresentationLocation => "its IIIF location names the identifier",
                WorkMatchStrength.Mets => "the identifier is its mets identifier",
                WorkMatchStrength.SierraSystemNumber => "the identifier is its Sierra system number",
                WorkMatchStrength.CalmRefNo => "the identifier is its CALM RefNo",
                WorkMatchStrength.CalmAltRefNo => "the identifier is its CALM AltRefNo",
                WorkMatchStrength.OtherIdentifier => "the identifier is one of its other identifiers",
                _ => "nothing on the work shows how it matched"
            };
        }
    }
}
