namespace Wellcome.Dds.Catalogue
{
    public class Identifier : CatalogueEntity
    {
        public LabelledEntity IdentifierType { get; set; }
        public string Value { get; set; }
    }
}
