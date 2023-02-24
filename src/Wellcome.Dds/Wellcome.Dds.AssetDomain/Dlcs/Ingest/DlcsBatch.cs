using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    /// <summary>
    /// A Database batch for saving info about a DLCS batch or patch operation
    /// </summary>
    public class DlcsBatch
    {
        public int Id { get; set; }
        public int DlcsIngestJobId { get; set; }
        public DateTime? RequestSent { get; set; }
        public string? RequestBody { get; set; }
        public DateTime? Finished { get; set; }
        public string? ResponseBody { get; set; }
        public int ErrorCode { get; set; }
        public string? ErrorText { get; set; }
        public int BatchSize { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? ContentLength { get; set; }        
    }
}
