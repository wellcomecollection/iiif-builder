namespace Wellcome.Dds.AssetDomain.Dlcs
{
    public class Page<T>
    {
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public T[] Items { get; set; }
    }
}
