using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Ghostscript.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Utils.Aws.S3;
using Wellcome.Dds.Common;

namespace PdfThumbGenerator
{
    /// <summary>
    /// Wrapper round GhostScript executable call to generate an image from the first page of a PDF
    /// </summary>
    public class PdfThumbnailUtil
    {
        private readonly ILogger<PdfThumbnailUtil> logger;
        private readonly IAmazonS3 ddsServiceS3;
        private readonly DdsOptions ddsOptions;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="s3ClientFactory"></param>
        /// <param name="ddsOptions"></param>
        /// <param name="logger"></param>
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
        /// 
        /// </summary>
        /// <param name="pdfStreamSource"></param>
        /// <param name="identifier"></param>
        public async Task EnsurePdfThumbnails(Func<Task<Stream>> pdfStreamSource, string identifier)
        {
            var folder = Path.Combine(Path.GetTempPath(), "pdf_thumbs");

            var inputPdfPath = Path.Combine(folder, $"{identifier}_source.pdf");
            var outputJpgPath = Path.Combine(folder, $"{identifier}_extracted.jpg");

            // Do we already have a big thumbnail for this b number?
            string key = $"_pdf_thumbs/{identifier}.jpg";

            // if thumb already exists, nothing to do
            if (await DoesObjectExist(key))
            {
                logger.LogInformation("{Identifier} already exists in s3, no-op", identifier);
                return;
            }

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            
            await SavePdfToDisk(pdfStreamSource, inputPdfPath, identifier);
            await GenerateJpgFromPdf(outputJpgPath, inputPdfPath, identifier);
            await ResizeImageToS3(outputJpgPath, key, identifier);
            
            if (File.Exists(inputPdfPath)) File.Delete(inputPdfPath);
            if (File.Exists(outputJpgPath)) File.Delete(outputJpgPath);
        }
        
        private async Task SavePdfToDisk(Func<Task<Stream>> pdfStreamSource, string inputPdfPath,
            string identifier)
        {
            logger.LogInformation("Saving {Identifier} to disk at {Path}", identifier, inputPdfPath);
            var pdfInputStream = await pdfStreamSource();
            pdfInputStream.Seek(0, SeekOrigin.Begin);

            var pdfFs = File.OpenWrite(inputPdfPath);
            await pdfInputStream.CopyToAsync(pdfFs);
            pdfFs.Close();
        }

        private async Task GenerateJpgFromPdf(string outputJpgPath, string inputPdfPath, string identifier)
        {
            logger.LogInformation("Generating JPG for {Identifier}, to disk at {Path}", identifier, outputJpgPath);
            
            var version = GhostscriptVersionInfo.GetLastInstalledVersion();
            var exePath = Path.Combine(version.LibPath.Split(";")[0], "gswin64.exe");
            var args =
                $"-dNOPAUSE -dBATCH -r96 -sDEVICE=jpeg -sOutputFile=\"{outputJpgPath}\" -dLastPage=1 \"{inputPdfPath}\"";
            logger.LogInformation("Calling ghostscript with args {Args}", args);

            using var ghostScriptProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                }
            };
            ghostScriptProcess.Start();
            var output = await ghostScriptProcess.StandardOutput.ReadToEndAsync();
            logger.LogInformation(output);
            await ghostScriptProcess.WaitForExitAsync();
        }

        private async Task ResizeImageToS3(string outputJpgPath, string key, string identifier)
        {
            logger.LogInformation("Resizing {Identifier} and saving to S3 at {Key}", identifier, key);
            using var image = await Image.LoadAsync(outputJpgPath);
            image.Mutate(x => x.Resize(1024, 0, KnownResamplers.Lanczos3));

            await using var jpegOutputStream = new MemoryStream();
            await image.SaveAsJpegAsync(jpegOutputStream);

            var putReqImg = new PutObjectRequest
            {
                InputStream = jpegOutputStream,
                ContentType = "image/jpeg",
                BucketName = ddsOptions.PresentationContainer,
                Key = key
            };

            await ddsServiceS3.PutObjectAsync(putReqImg);
        }

        private async Task<bool> DoesObjectExist(string key)
        {
            var getReq = new GetObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key
            };
            
            try
            {
                using var res = await ddsServiceS3.GetObjectAsync(getReq);
                if (res.HttpStatusCode == System.Net.HttpStatusCode.OK && res.ContentLength > 0)
                    return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting object at {Key}", key);
            }
            
            return false;
        }
    }
}