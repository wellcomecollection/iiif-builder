namespace Wellcome.Dds.Catalogue
{
    public class Item : CatalogueEntity
    {
        public string Id { get; set; }
        public Identifier[] Identifiers { get; set; }
        public Location[] Locations { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
