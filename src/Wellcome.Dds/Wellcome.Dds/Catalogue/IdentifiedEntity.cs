namespace Wellcome.Dds.Catalogue
{
    public class IdentifiedEntity : LabelledEntity
    {
        public Identifier[] Identifiers { get; set; }
        
        public override string ToString()
        {
            return $"{(Id ?? "-")}: {Label}";
        }
    }
}
