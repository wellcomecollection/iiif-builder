using System;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public class ImageIngestResult
    {
        public Batch[]? CloudBatchRegistrationResponse { get; set; }

        public static ImageIngestResult Empty => new()
            {
                CloudBatchRegistrationResponse = Array.Empty<Batch>()
            };
    }
}
