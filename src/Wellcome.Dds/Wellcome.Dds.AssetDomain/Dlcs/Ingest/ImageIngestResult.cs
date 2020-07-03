using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public class ImageIngestResult
    {
        public Batch[] CloudBatchRegistrationResponse { get; set; }

        public static ImageIngestResult Empty
        {
            get
            {
                return new ImageIngestResult
                {
                    CloudBatchRegistrationResponse = new Batch[0]
                };
            }
        }
    }
}
