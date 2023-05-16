namespace Wellcome.Dds.Catalogue
{
    public class WorkResultPage : CatalogueEntity
    {
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalResults { get; set; }
        public Work[]? Results { get; set; }
        public string? PrevPage { get; set; }
        public string? NextPage { get; set; }
    }
}
