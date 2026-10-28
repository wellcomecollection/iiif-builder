using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Utils.Aws.S3;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Images;

namespace Wellcome.Dds.Repositories.Pdf;

public class PdfThumbnailService : IPdfThumbnailService
{
    private const string PdfMimeType = "application/pdf";
    private const string KeyPrefix = "_pdf_thumbs/";

    private readonly IMetsRepository metsRepository;
    private readonly IIdentityService identityService;
    private readonly IPdfRasteriser rasteriser;
    private readonly IAmazonS3 ddsServiceS3;
    private readonly DdsOptions ddsOptions;
    private readonly PdfThumbnailOptions options;
    private readonly ILogger<PdfThumbnailService> logger;

    public PdfThumbnailService(
        IMetsRepository metsRepository,
        IIdentityService identityService,
        IPdfRasteriser rasteriser,
        INamedAmazonS3ClientFactory s3ClientFactory,
        IOptions<DdsOptions> ddsOptions,
        IOptions<PdfThumbnailOptions> options,
        ILogger<PdfThumbnailService> logger)
    {
        this.metsRepository = metsRepository;
        this.identityService = identityService;
        this.rasteriser = rasteriser;
        ddsServiceS3 = s3ClientFactory.Get(NamedClient.Dds);
        this.ddsOptions = ddsOptions.Value;
        this.options = options.Value;
        this.logger = logger;
    }

    public string GetKey(string identifier)
    {
        return $"{KeyPrefix}{identifier}.jpg";
    }

    public async Task<Stream?> GetThumbnail(string identifier)
    {
        var key = GetKey(identifier);
        try
        {
            var response = await ddsServiceS3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key
            });
            if (response.HttpStatusCode == HttpStatusCode.OK && response.ContentLength > 0)
            {
                return response.ResponseStream;
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

    public async Task<bool> ThumbnailExists(string identifier)
    {
        try
        {
            var metadata = await ddsServiceS3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = GetKey(identifier)
            });
            return metadata.HttpStatusCode == HttpStatusCode.OK && metadata.ContentLength > 0;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<PdfThumbnailResult> EnsureThumbnail(DdsIdentity ddsId, bool regenerate = false, CancellationToken cancellationToken = default)
    {
        var identifier = ddsId.Value;
        var key = GetKey(identifier);
        if (!regenerate && await ThumbnailExists(identifier))
        {
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.AlreadyExists);
        }

        IManifestation? manifestation;
        try
        {
            manifestation = await metsRepository.GetAsync(ddsId) as IManifestation;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load METS for {Identifier}", identifier);
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.Failed, $"Could not load METS: {ex.Message}");
        }
        if (manifestation == null)
        {
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.NoPdf,
                "Not a manifestation (a collection has no PDF of its own; use a manifestation identifier)");
        }

        var pdfFile = manifestation.Sequence.AnyItems().FirstOrDefault(pf => pf.MimeType == PdfMimeType);
        if (pdfFile?.RelativePath == null)
        {
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.NoPdf, "The manifestation contains no PDF");
        }

        var declaredSize = ParseSize(pdfFile.AssetMetadata?.GetFileSize());
        if (declaredSize > options.MaxPdfBytes)
        {
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.TooLarge,
                $"PDF is {StringUtils.FormatFileSize(declaredSize)}, above the {StringUtils.FormatFileSize(options.MaxPdfBytes)} limit")
            {
                SourcePath = pdfFile.RelativePath
            };
        }

        var workingDirectory = Path.Combine(Path.GetTempPath(), "pdf_thumbs", Guid.NewGuid().ToString("N"));
        var pdfPath = Path.Combine(workingDirectory, "source.pdf");
        var renderedPath = Path.Combine(workingDirectory, "page1.jpg");
        try
        {
            Directory.CreateDirectory(workingDirectory);

            logger.LogInformation("Fetching {RelativePath} for {Identifier}", pdfFile.RelativePath, identifier);
            var tooLarge = await DownloadWithCap(pdfFile, pdfPath, cancellationToken);
            if (tooLarge)
            {
                return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.TooLarge,
                    $"PDF exceeds the {StringUtils.FormatFileSize(options.MaxPdfBytes)} limit")
                {
                    SourcePath = pdfFile.RelativePath
                };
            }

            logger.LogInformation("Rendering page 1 of {Identifier} with {Rasteriser}", identifier, rasteriser.Name);
            await rasteriser.RasteriseFirstPage(pdfPath, renderedPath, cancellationToken);

            var jpeg = JpegResizer.ResizeToWidth(renderedPath, options.ThumbnailWidth, options.JpegQuality);

            await using var jpegStream = new MemoryStream(jpeg);
            await ddsServiceS3.PutObjectAsync(new PutObjectRequest
            {
                InputStream = jpegStream,
                ContentType = "image/jpeg",
                BucketName = ddsOptions.PresentationContainer,
                Key = key
            }, cancellationToken);

            logger.LogInformation("Stored PDF thumbnail {Key} ({Size})", key, StringUtils.FormatFileSize(jpeg.Length));
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.Generated,
                $"{StringUtils.FormatFileSize(jpeg.Length)} JPEG from {rasteriser.Name}")
            {
                SourcePath = pdfFile.RelativePath
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF thumbnail generation failed for {Identifier}", identifier);
            return new PdfThumbnailResult(identifier, key, PdfThumbnailOutcome.Failed, ex.Message)
            {
                SourcePath = pdfFile.RelativePath
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not remove {Directory}", workingDirectory);
            }
        }
    }

    public async Task<List<PdfThumbnailResult>> EnsureThumbnailsForPackage(DdsIdentity packageId, bool regenerate = false, CancellationToken cancellationToken = default)
    {
        var results = new List<PdfThumbnailResult>();
        await foreach (var inContext in metsRepository.GetAllManifestationsInContext(packageId).WithCancellation(cancellationToken))
        {
            var manifestation = inContext.Manifestation;
            var manifestationId = identityService.GetIdentity(manifestation.Identifier);
            if (manifestation.Partial)
            {
                manifestation = await metsRepository.GetAsync(manifestationId) as IManifestation;
            }
            if (manifestation?.Sequence.AnyItems().Any(pf => pf.MimeType == PdfMimeType) != true)
            {
                continue;
            }
            results.Add(await EnsureThumbnail(manifestationId, regenerate, cancellationToken));
        }
        return results;
    }

    /// <summary>
    /// Copy the PDF to disk, giving up as soon as it exceeds the size limit. Returns true if it did.
    /// </summary>
    private async Task<bool> DownloadWithCap(IPhysicalFile pdfFile, string pdfPath, CancellationToken cancellationToken)
    {
        await using var source = await pdfFile.WorkStore.GetStreamForPathAsync(pdfFile.RelativePath!);
        await using var target = File.Create(pdfPath);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > options.MaxPdfBytes)
            {
                return true;
            }
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return false;
    }

    private static long ParseSize(string? size)
    {
        return long.TryParse(size, out var bytes) ? bytes : 0;
    }
}
