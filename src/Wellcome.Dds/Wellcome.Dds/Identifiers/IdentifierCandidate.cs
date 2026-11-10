namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// One lookup that an arbitrary identifier string might resolve through.
    /// Either a forward lookup (SourceSystem is null and Value is a canonical id) or a reverse lookup.
    /// </summary>
    public class IdentifierCandidate
    {
        public IdentifierCandidate(string value, string? sourceSystem, string type, string reason)
        {
            Value = value;
            SourceSystem = sourceSystem;
            Type = type;
            Reason = reason;
        }

        /// <summary>
        /// The value to send to the API; may have been normalised from the supplied string (e.g., a b number
        /// with its check digit completed).
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Null for a forward (canonical id) lookup.
        /// </summary>
        public string? SourceSystem { get; }

        public string Type { get; }

        /// <summary>
        /// Why this candidate was generated, for display.
        /// </summary>
        public string Reason { get; }

        public bool IsCanonical => SourceSystem == null;

        public override string ToString()
        {
            return IsCanonical ? $"canonical: {Value}" : $"{SourceSystem}: {Value} ({Type})";
        }
    }
}
