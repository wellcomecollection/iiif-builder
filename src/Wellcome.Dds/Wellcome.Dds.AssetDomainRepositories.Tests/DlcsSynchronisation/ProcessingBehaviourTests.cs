using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.DlcsSynchronisation;

public class ProcessingBehaviourTests
{
    private StoredFile MakeStoredFile()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile> { new StoredFile{PhysicalFile = pf } };
        return pf.Files[0] as StoredFile;
    }
    
    private PhysicalFile MakePhysicalFile()
    {
        return new PhysicalFile(A.Fake<ArchiveStorageServiceWorkStore>(), "dummy");
    }
    
    [Fact]
    public void Image_Has_IIIF_and_thumbs_Delivery_Channel()
    {
        var storedFile = MakeStoredFile();
        storedFile.MimeType = "image/jpg";

        var processing = storedFile.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-img");
    }
    
    
    [Fact]
    public void MP3_Has_File_Delivery_Channel()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "audio/x-mpeg-3";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "file");
    }
    
    
    [Fact]
    public void MP3_Has_None_IOP()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "audio/x-mpeg-3";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().Be("none");
    }
    
    
    [Fact]
    public void Wav_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "audio/wav";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
    
    [Fact]
    public void Wav_Has_Null_IOP()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "audio/wav";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void mpeg_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "video/mpeg-2";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
        
    [Fact]
    public void mpeg_Has_Null_IOP()
    {
        var pf = MakeStoredFile();
        pf.MimeType = "video/mpeg-2";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void Normal_MP4_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf }
        };
        pf.MimeType = "video/mp4";

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
        
    [Fact]
    public void Normal_MP4_Has_Null_IOP()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf }  // Mp4 on its own
        };
        pf.MimeType = "video/mp4";

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void Access_MP4_720_Has_File_Delivery_Channel()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf },       // the access copy
            new StoredFile { MimeType = "application/mxf", PhysicalFile = pf }  // the master
        };
        pf.MimeType = "video/mp4";
        SetHeight(pf.Files[0], 720);

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "file");
    }
    [Fact]
    public void Access_MP4_1440_Has_iiif_av_and_file_Delivery_Channel()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf},       // the access copy
            new StoredFile { MimeType = "application/mxf", PhysicalFile = pf }  // the master
        };
        pf.MimeType = "video/mp4";
        SetHeight(pf.Files[0], 1440);

        var processing = pf.Files[0].ProcessingBehaviour;

        var expected = new HashSet<string> { "iiif-av", "file" };
        processing.DeliveryChannels.Should().BeEquivalentTo(expected);
    }
    

    private static void SetHeight(IStoredFile sf, int height)
    {
        var assetMetadata = A.Fake<IAssetMetadata>();
        A.CallTo(() => assetMetadata.GetMediaDimensions()).Returns(new MediaDimensions { Height = height });
        sf.AssetMetadata = assetMetadata;
    }

    [Fact]
    public void Access_MP4_720_Has_None_IOP()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf },       // the access copy
            new StoredFile { MimeType = "application/mxf", PhysicalFile = pf }  // the master
        };
        pf.MimeType = "video/mp4";
        SetHeight(pf.Files[0], 720);

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().Be("none");
    }
    
    [Fact]
    public void Access_MP4_1440_Has_Null_IOP()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf },       // the access copy
            new StoredFile { MimeType = "application/mxf", PhysicalFile = pf }  // the master
        };
        pf.MimeType = "video/mp4";
        SetHeight(pf.Files[0], 1440);

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    // Use this to test resolution-specific transcoding - needs the logic in ProcessingBehaviour to change
    
    [Fact]
    public void Resolution_determines_transcode()
    {
        var pf = MakePhysicalFile();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4", PhysicalFile = pf }
        };
        pf.MimeType = "video/mp4";
        SetHeight(pf.Files[0], 1440);

        var processing = pf.Files[0].ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
        // processing.ImageOptimisationPolicy.Should().Be("QHD");
        // processing.ImageOptimisationPolicy.Should().Be("HIGHER");
    }
}