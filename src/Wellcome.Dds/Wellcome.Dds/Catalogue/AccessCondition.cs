namespace Wellcome.Dds.Catalogue
{
    public class AccessCondition : CatalogueEntity
    {
        public LabelledEntity? Status { get; set; }
        
        public override string? ToString()
        {
            return Status?.Label;
        }
    }
}
