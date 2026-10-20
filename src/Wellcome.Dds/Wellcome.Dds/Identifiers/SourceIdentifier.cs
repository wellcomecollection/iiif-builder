using System;

namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// A single source identifier mapped to a canonical id.
    /// </summary>
    public class SourceIdentifier
    {
        /// <summary>
        /// Ontology type for this row. Per-row because a canonical id can carry rows of differing types.
        /// </summary>
        public string? Type { get; set; }
        public string? SourceSystem { get; set; }
        public string? Value { get; set; }

        /// <summary>
        /// False for the original (earliest createdAt), true for inherited predecessor aliases.
        /// </summary>
        public bool IsAlias { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public override string ToString()
        {
            return $"{SourceSystem ?? "-"}: {Value}";
        }
    }
}
