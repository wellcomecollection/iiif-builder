using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Ghostscript.NET.Rasterizer;
using IIIF;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils.Aws.S3;
using Wellcome.Dds;
using Wellcome.Dds.Common;

namespace PdfService
{
    public class PdfThumbnailUtil : IPdfThumbnailServices
    {
        private readonly IAmazonS3 ddsServiceS3;
        private readonly DdsOptions ddsOptions;
        
        public PdfThumbnailUtil(
            INamedAmazonS3ClientFactory s3ClientFactory,
            IOptions<DdsOptions> ddsOptions)
        {
            ddsServiceS3 = s3ClientFactory.Get(NamedClient.Dds);
            this.ddsOptions = ddsOptions.Value;
        }
        
        public async Task<List<Size>> EnsurePdfThumbnails(Func<Task<Stream>> pdfStreamSource, int[] thumbSizes, string identifier)
        {
            List<Size> actualSizes;
            // Do we already have a big thumbnail for this b number?
            var getReq = new GetObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = $"_pdf_thumbs/{identifier}.json"
            };

            try
            {
                using (GetObjectResponse response = await ddsServiceS3.GetObjectAsync(getReq))
                {
                    var serializer = new JsonSerializer();
                    using (var sr = new StreamReader(response.ResponseStream))
                    using (var jsonTextReader = new JsonTextReader(sr))
                    {
                        var sizes = serializer.Deserialize<List<Size>>(jsonTextReader);
                        sizes.RemoveAt(0);
                        return sizes;
                    }
                }

            }
            catch
            {
                // no sizes in S3
            }
            
            // if not, create one
            using (var rasterizer = new GhostscriptRasterizer())
            {
                var pdfInputStream = await pdfStreamSource();
                rasterizer.Open(pdfInputStream);
                var img = rasterizer.GetPage(96, 96, 1);
                var jpegOutputStream = new MemoryStream();
                img.Save(jpegOutputStream, ImageFormat.Jpeg);
                var putReqImg = new PutObjectRequest
                {
                    InputStream = jpegOutputStream,
                    ContentType = "image/jpeg",
                    BucketName = ddsOptions.PresentationContainer,
                    Key = $"_pdf_thumbs/{identifier}.jpg"
                };
                await ddsServiceS3.PutObjectAsync(putReqImg);

                var allSizes = new List<Size> {new(img.Width, img.Height)};
                actualSizes = thumbSizes.Select(dim => Size.Confine(dim, allSizes[0])).ToList();
                allSizes.AddRange(actualSizes);
                var putReqSizes = new PutObjectRequest
                {
                    ContentBody = JsonConvert.SerializeObject(allSizes),
                    ContentType = "application/json",
                    BucketName = ddsOptions.PresentationContainer,
                    Key = $"_pdf_thumbs/{identifier}.json"
                };
                await ddsServiceS3.PutObjectAsync(putReqSizes);
            }

            return actualSizes;
        }
    }
}