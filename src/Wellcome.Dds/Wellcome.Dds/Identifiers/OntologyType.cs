namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// The ontology types the Identifiers API serves. The registry holds other types (e.g., Concept)
    /// but the API omits them; the enum is extended on demand (RFC 089 open question 6).
    /// </summary>
    public static class OntologyType
    {
        public const string Work = "Work";
        public const string Image = "Image";
        public const string Item = "Item";

        public static readonly string[] All = { Work, Image, Item };
    }
}
