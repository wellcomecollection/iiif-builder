using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils.Aws.S3;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server
{
    public class PdfThumbnailUtil
    {
        private readonly ILogger<PdfThumbnailUtil> logger;
        private readonly IAmazonS3 ddsServiceS3;
        private readonly DdsOptions ddsOptions;

        public PdfThumbnailUtil(
            INamedAmazonS3ClientFactory s3ClientFactory,
            IOptions<DdsOptions> ddsOptions,
            ILogger<PdfThumbnailUtil> logger)
        {
            this.logger = logger;
            ddsServiceS3 = s3ClientFactory.Get(NamedClient.Dds);
            this.ddsOptions = ddsOptions.Value;
        }

        /// <summary>
        /// Get thumbnail of PDF with specified identifier.
        /// </summary>
        /// <param name="identifier">BNumber of </param>
        /// <returns>Stream if found, else null.</returns>
        public async Task<Stream> GetPdfThumbnail(DdsIdentifier identifier)
        {
            var key = $"_pdf_thumbs/{identifier}.jpg";

            var getReq = new GetObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key
            };

            try
            {
                var res = await ddsServiceS3.GetObjectAsync(getReq);
                if (res.HttpStatusCode == HttpStatusCode.OK && res.ContentLength > 0)
                {
                    return res.ResponseStream;
                }
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (AmazonS3Exception ex)
            {
                logger.LogError(ex, "Error getting object at {Key}", key);
            }

            return null;
        }
    }
}