namespace Wellcome.Dds.Dashboard.Models
{
    public class SyncSummary
    {
        public string CssClass { get; set; }
        public SyncCategory[] Categories { get; set; }
        public AccessSummary[] AccessConditions { get; set; }
    }

    public class SyncCategory
    {
        public string StatusIcon { get; set; }
        public int Count { get; set; }
        public string Label { get; set; }
        public string TableRowId { get; set; }
    }

    public class AccessSummary
    {
        public string StatusIcon { get; set; }
        public int Count { get; set; }
        public string Label { get; set; }
        public string TableRowId { get; set; }
    }
}