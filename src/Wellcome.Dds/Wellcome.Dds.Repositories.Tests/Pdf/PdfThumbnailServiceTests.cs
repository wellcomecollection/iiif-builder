using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;
using Utils.Aws.S3;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Pdf;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Pdf;

public class PdfThumbnailServiceTests
{
    private const string Bucket = "presentation-bucket";
    private const string Identifier = "ARTCOO/B/15";
    private const string ExpectedKey = "_pdf_thumbs/ARTCOO/B/15.jpg";

    private readonly IMetsRepository metsRepository = A.Fake<IMetsRepository>();
    private readonly IIdentityService identityService = A.Fake<IIdentityService>();
    private readonly IPdfRasteriser rasteriser = A.Fake<IPdfRasteriser>();
    private readonly IAmazonS3 s3 = A.Fake<IAmazonS3>();
    private readonly PdfThumbnailOptions options = new();
    private readonly PdfThumbnailService sut;
    private readonly DdsIdentity ddsId = ParsingIdentityService.Parse(Identifier);

    public PdfThumbnailServiceTests()
    {
        var s3Factory = A.Fake<INamedAmazonS3ClientFactory>();
        A.CallTo(() => s3Factory.Get(NamedClient.Dds)).Returns(s3);
        A.CallTo(() => identityService.GetIdentity(A<string>._)).ReturnsLazily((string s) => ParsingIdentityService.Parse(s));
        A.CallTo(() => rasteriser.Name).Returns("fake");
        // default: no thumbnail stored yet
        A.CallTo(() => s3.GetObjectMetadataAsync(A<GetObjectMetadataRequest>._, A<CancellationToken>._))
            .Throws(new AmazonS3Exception("not found", ErrorType.Sender, "NoSuchKey", "req", HttpStatusCode.NotFound));
        // default: the rasteriser writes a portrait A4 PNG at 150 dpi, as Ghostscript would
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string pdf, string png, CancellationToken _) => WritePng(png, 1240, 1754))
            .Returns(Task.CompletedTask);

        sut = new PdfThumbnailService(metsRepository, identityService, rasteriser, s3Factory,
            Options.Create(new DdsOptions { PresentationContainer = Bucket }),
            Options.Create(options),
            NullLogger<PdfThumbnailService>.Instance);
    }

    private static void WritePng(string path, int width, int height)
    {
        using var image = (Image.Black(width, height, bands: 3) + 200).Cast(Enums.BandFormat.Uchar);
        image.WriteToFile(path);
    }

    private IManifestation ManifestationWith(params (string mimeType, string relativePath, string? size)[] files)
    {
        return ManifestationWith(AccessCondition.Open, files);
    }

    private IManifestation ManifestationWith(string accessCondition, params (string mimeType, string relativePath, string? size)[] files)
    {
        var manifestation = A.Fake<IManifestation>();
        var sequence = new List<IPhysicalFile>();
        foreach (var (mimeType, relativePath, size) in files)
        {
            var workStore = A.Fake<IWorkStore>();
            A.CallTo(() => workStore.GetStreamForPathAsync(relativePath))
                .ReturnsLazily(() => Task.FromResult<Stream>(new MemoryStream(TinyPdf.SinglePage(595, 842))));
            var file = A.Fake<IPhysicalFile>();
            file.MimeType = mimeType;
            file.RelativePath = relativePath;
            file.WorkStore = workStore;
            file.AccessCondition = accessCondition;
            if (size != null)
            {
                var metadata = A.Fake<IAssetMetadata>();
                A.CallTo(() => metadata.GetFileSize()).Returns(size);
                file.AssetMetadata = metadata;
            }
            sequence.Add(file);
        }
        manifestation.Sequence = sequence;
        A.CallTo(() => metsRepository.GetAsync(A<DdsIdentity>.That.Matches(d => d.Value == Identifier)))
            .Returns(manifestation);
        return manifestation;
    }

    private PutObjectRequest? CapturedPut()
    {
        var call = Fake.GetCalls(s3).Matching<IAmazonS3>(x => x.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._));
        foreach (var c in call)
        {
            return c.GetArgument<PutObjectRequest>(0);
        }
        return null;
    }

    [Fact]
    public async Task Generates_Resizes_And_Stores_The_First_Page()
    {
        ManifestationWith(("text/plain", "objects/readme.txt", null), ("application/pdf", "objects/report.pdf", "12345"));
        byte[]? stored = null;
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
            .Invokes((PutObjectRequest req, CancellationToken _) =>
            {
                using var ms = new MemoryStream();
                req.InputStream.CopyTo(ms);
                stored = ms.ToArray();
            })
            .Returns(Task.FromResult(new PutObjectResponse()));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.Generated);
        result.Key.Should().Be(ExpectedKey);
        result.SourcePath.Should().Be("objects/report.pdf");
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>.That.EndsWith("source.pdf"), A<string>.That.EndsWith("page1.png"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        var put = CapturedPut();
        put.Should().NotBeNull();
        put!.BucketName.Should().Be(Bucket);
        put.Key.Should().Be(ExpectedKey);
        put.ContentType.Should().Be("image/jpeg");
        put.Metadata[PdfThumbnailService.SourceMetadataKey].Should().Be(PdfThumbnailService.SourceMetadataGenerated);
        // Resized to the thumbnail width, portrait, not fitted into a square
        using var image = Image.JpegloadBuffer(stored!);
        image.Width.Should().Be(options.ThumbnailWidth);
        image.Height.Should().Be(1448);
    }

    private void StoredThumbnail(bool generated)
    {
        var response = new GetObjectMetadataResponse { HttpStatusCode = HttpStatusCode.OK, ContentLength = 4096 };
        if (generated)
        {
            response.Metadata.Add(PdfThumbnailService.SourceMetadataKey, PdfThumbnailService.SourceMetadataGenerated);
        }
        A.CallTo(() => s3.GetObjectMetadataAsync(A<GetObjectMetadataRequest>._, A<CancellationToken>._)).Returns(response);
    }

    [Fact]
    public async Task Keeps_An_Existing_Thumbnail_Unless_Asked_To_Replace_It()
    {
        ManifestationWith(("application/pdf", "objects/report.pdf", null));
        StoredThumbnail(generated: true);

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.AlreadyExists);
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustNotHaveHappened();

        var regenerated = await sut.EnsureThumbnail(ddsId, PdfThumbnailRefresh.ReplaceAll);

        regenerated.Outcome.Should().Be(PdfThumbnailOutcome.Generated);
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Replacing_Generated_Thumbnails_Spares_One_Uploaded_By_Hand()
    {
        ManifestationWith(("application/pdf", "objects/report.pdf", null));
        StoredThumbnail(generated: false);

        var kept = await sut.EnsureThumbnail(ddsId, PdfThumbnailRefresh.ReplaceGenerated);

        kept.Outcome.Should().Be(PdfThumbnailOutcome.AlreadyExists);
        kept.Message.Should().Contain("uploaded by hand");
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustNotHaveHappened();

        StoredThumbnail(generated: true);
        var replaced = await sut.EnsureThumbnail(ddsId, PdfThumbnailRefresh.ReplaceGenerated);

        replaced.Outcome.Should().Be(PdfThumbnailOutcome.Generated);
    }

    [Theory]
    [InlineData(AccessCondition.Closed)]
    [InlineData(AccessCondition.RestrictedFiles)]
    [InlineData(AccessCondition.ClinicalImages)]
    [InlineData(AccessCondition.Missing)]
    [InlineData(null)]
    public async Task A_Pdf_That_Is_Not_Publicly_Viewable_Gets_No_Thumbnail(string? accessCondition)
    {
        ManifestationWith(accessCondition!, ("application/pdf", "objects/private.pdf", null));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.NotPermitted);
        result.ThumbnailAvailable.Should().BeFalse();
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AccessCondition.Open)]
    [InlineData(AccessCondition.RequiresRegistration)]
    [InlineData(AccessCondition.OpenWithAdvisory)]
    public async Task A_Publicly_Viewable_Pdf_Gets_A_Thumbnail(string accessCondition)
    {
        ManifestationWith(accessCondition, ("application/pdf", "objects/public.pdf", null));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.Generated);
    }

    [Fact]
    public async Task An_S3_Error_Other_Than_Not_Found_Counts_As_No_Thumbnail_And_Does_Not_Throw()
    {
        A.CallTo(() => s3.GetObjectMetadataAsync(A<GetObjectMetadataRequest>._, A<CancellationToken>._))
            .Throws(new AmazonS3Exception("forbidden", ErrorType.Sender, "AccessDenied", "req", HttpStatusCode.Forbidden));

        (await sut.ThumbnailExists(Identifier)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Manifestation_Without_A_Pdf_Is_NoPdf()
    {
        ManifestationWith(("image/jp2", "objects/page.jp2", null));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.NoPdf);
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Collection_Is_NoPdf_With_An_Explanation()
    {
        A.CallTo(() => metsRepository.GetAsync(A<DdsIdentity>._)).Returns(A.Fake<ICollection>());

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.NoPdf);
        result.Message.Should().Contain("collection");
    }

    [Fact]
    public async Task A_Pdf_Declared_Too_Large_Is_Not_Fetched()
    {
        options.MaxPdfBytes = 1000;
        ManifestationWith(("application/pdf", "objects/huge.pdf", "5000"));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.TooLarge);
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Pdf_That_Turns_Out_Too_Large_While_Downloading_Is_TooLarge()
    {
        // no declared size, and the tiny PDF is bigger than this limit
        options.MaxPdfBytes = 100;
        ManifestationWith(("application/pdf", "objects/undeclared.pdf", null));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.TooLarge);
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Rasteriser_Failure_Is_Reported_Not_Thrown_And_Nothing_Is_Stored()
    {
        ManifestationWith(("application/pdf", "objects/broken.pdf", null));
        A.CallTo(() => rasteriser.RasteriseFirstPage(A<string>._, A<string>._, A<CancellationToken>._))
            .Throws(new PdfRasteriseException("pdftoppm exited with 1 and no output"));

        var result = await sut.EnsureThumbnail(ddsId);

        result.Outcome.Should().Be(PdfThumbnailOutcome.Failed);
        result.Message.Should().Contain("pdftoppm exited");
        A.CallTo(() => s3.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public void The_Key_Matches_What_The_Dashboard_Upload_And_The_Thumb_Endpoint_Use()
    {
        sut.GetKey("b29696586").Should().Be("_pdf_thumbs/b29696586.jpg");
        sut.GetKey(Identifier).Should().Be(ExpectedKey);
    }
}
