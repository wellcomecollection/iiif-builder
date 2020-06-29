using System;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public class IngestAction
    {
        public int Id { get; set; }
        public string ManifestationId { get; set; }
        public int? JobId { get; set; }
        public string Username { get; set; }
        public string Description { get; set; }
        public string Action { get; set; }
        public DateTime Performed { get; set; }
    }
}
