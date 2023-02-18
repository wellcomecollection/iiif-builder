using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.DlcsSynchronisation;

public class ProcessingBehaviourTests
{
    [Fact]
    public void Image_Has_IIIF_and_thumbs_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "image/jpg";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-img" || s == "thumbs");
    }
    
    
    [Fact]
    public void MP3_Has_File_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "audio/x-mpeg-3";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "file");
    }
    
    
    [Fact]
    public void MP3_Has_None_IOP()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "audio/x-mpeg-3";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().Be("none");
    }
    
    
    [Fact]
    public void Wav_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "audio/wav";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
    
    [Fact]
    public void Wav_Has_Null_IOP()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "audio/wav";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void mpeg_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "video/mpeg-2";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
        
    [Fact]
    public void mpeg_Has_Null_IOP()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.MimeType = "video/mpeg-2";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void Normal_MP4_Has_IIIF_AV_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4" }
        };
        pf.MimeType = "video/mp4";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "iiif-av");
    }    
        
    [Fact]
    public void Normal_MP4_Has_Null_IOP()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4" }  // Mp4 on its own
        };
        pf.MimeType = "video/mp4";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
    }
    
    [Fact]
    public void Access_MP4_Has_File_Delivery_Channel()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4" },       // the access copy
            new StoredFile { MimeType = "application/mxf" }  // the master
        };
        pf.MimeType = "video/mp4";

        var processing = pf.ProcessingBehaviour;

        processing.DeliveryChannels.Should().OnlyContain(s => s == "file");
    }    
        
    [Fact]
    public void Access_MP4_Has_None_IOP()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4" },       // the access copy
            new StoredFile { MimeType = "application/mxf" }  // the master
        };
        pf.MimeType = "video/mp4";

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().Be("none");
    }
    
    // Use this to test resolution-specific transcoding - needs the logic in ProcessingBehaviour to change
    
    [Fact]
    public void Resolution_determines_transcode()
    {
        var pf = A.Fake<PhysicalFile>();
        pf.Files = new List<IStoredFile>
        {
            new StoredFile { MimeType = "video/mp4" }
        };
        pf.MimeType = "video/mp4";
        var assetMetadata = A.Fake<IAssetMetadata>();
        A.CallTo(() => assetMetadata.GetMediaDimensions()).Returns(new MediaDimensions { Height = 1440 });
        pf.AssetMetadata = assetMetadata;

        var processing = pf.ProcessingBehaviour;

        processing.ImageOptimisationPolicy.Should().BeNull();
        // processing.ImageOptimisationPolicy.Should().Be("QHD");
        // processing.ImageOptimisationPolicy.Should().Be("HIGHER");
    }
}