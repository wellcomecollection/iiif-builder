namespace Wellcome.Dds.Catalogue
{
    public class Location : CatalogueEntity
    {
        public LabelledEntity LocationType { get; set; }
        public string Url { get; set; }
        public string Credit { get; set; }
        public License License { get; set; }
        public AccessCondition[] AccessConditions { get; set; }
    }
}
