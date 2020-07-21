namespace Wellcome.Dds.Catalogue
{
    public class Note : CatalogueEntity
    {
        public string[] Contents { get; set; }
        public LabelledEntity NoteType { get; set; }
    }
}
