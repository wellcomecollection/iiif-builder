namespace Wellcome.Dds.Catalogue
{
    public class LabelledEntity : CatalogueEntity
    {
        public string? Id { get; set; }
        public string? Label { get; set; }

        public override string ToString()
        {
            return $"{(Id ?? "-")}: {Label}";
        }
    }
}
