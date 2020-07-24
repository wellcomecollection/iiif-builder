
namespace Wellcome.Dds.Catalogue
{
    public class ProductionEvent : LabelledEntity
    {
        public IdentifiedEntity[] Places { get; set; }
        public IdentifiedEntity[] Agents { get; set; }
        public IdentifiedEntity[] Dates { get; set; }
    }
}
