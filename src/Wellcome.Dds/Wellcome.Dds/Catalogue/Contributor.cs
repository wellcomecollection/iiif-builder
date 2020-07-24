namespace Wellcome.Dds.Catalogue
{
    public class Contributor : CatalogueEntity
    {
        public IdentifiedEntity Agent { get; set; }
        public IdentifiedEntity[] Roles { get; set; }

        public override string ToString()
        {
            return Agent.ToString();
        }
    }
}
