using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Wellcome.Dds.Common.Tests;

public class LinkWriterTests
{
    private const string InitialDomain = "https://iiif.wellcomecollection.org";
    private const string NewDdsDomain = "https://rewritten-dds.org";
    private const string NewDlcsDomain = "https://rewritten-dlcs.org";
    private const int NewDlcsSpace = 6;

    private static readonly Dictionary<string, string> TestData;
    private static readonly string TestDataString;
    
    static LinkWriterTests()
    {
        TestData = new Dictionary<string, string>
        {
            ["manifest-id"] = $"{InitialDomain}/presentation/manifest",
            ["image-service-1"] = $"{InitialDomain}/image/image1.jp2",
            ["image-params-1"] = $"{InitialDomain}/image/image1.jp2/full/max/0/default.jpg",
            ["text-resource"] = "https://api.wellcomecollection.org/text/v1/text-resource",
            ["alto-resource"] = "https://api.wellcomecollection.org/text/alto/alto-resource",
            ["other-wc-api-link"] = "https://api.wellcomecollection.org/catalogue/v2/works/xxynxjmp",
            ["other-wc-link"] = "https://wellcomecollection.org/",
            ["other-external-link"] = "https://example.org/hello",
            ["image-service-2"] = $"{InitialDomain}/image/image2.jp2",
            ["image-params-2"] = $"{InitialDomain}/image/image2.jp2/full/max/0/default.jpg",
            ["canvas-id"] = $"{InitialDomain}/presentation/image1/canvases/canvas1",
            ["video-resource"] = $"{InitialDomain}/av/video.mp4",
            ["audio-resource"] = $"{InitialDomain}/av/sound.mp3",
            ["file-resource"] = $"{InitialDomain}/file/my-file",
            ["pdf-derivative"] = $"{InitialDomain}/pdf/my-book",
            ["auth-service"] = $"{InitialDomain}/auth/clickthrough",
            ["auth-logout"] = $"{InitialDomain}/auth/clickthrough/logout",
            ["thumb-service-1"] = $"{InitialDomain}/thumbs/image1.jp2",
            ["thumb-params-1"] = $"{InitialDomain}/thumbs/image1.jp2/full/max/0/default.jpg",
            ["other-dds"] = $"{InitialDomain}/x/y/z"
        };
        TestDataString = JsonSerializer.Serialize(TestData);
    }

    [Fact]
    public static void DDS_Only_Links_Rewritten()
    {
        // arrange
        var ddsOnlyOptions = new DdsOptions
        {
            LinkedDataDomain = InitialDomain,
            RewriteDomainLinksTo = NewDdsDomain
        };
        var ddsOnlyLinkRewriter = new LinkRewriter(Options.Create(ddsOnlyOptions));
        
        // act
        var rewritten = ddsOnlyLinkRewriter.RewriteLinks(TestDataString);
        
        // assert
        ddsOnlyLinkRewriter.RequiresRewriting().Should().BeTrue();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(rewritten);
        
        // expected to be transformed:
        dict["manifest-id"].Should().Be($"{NewDdsDomain}/presentation/manifest");
        dict["text-resource"].Should().Be($"{NewDdsDomain}/text/v1/text-resource");
        dict["alto-resource"].Should().Be($"{NewDdsDomain}/text/alto/alto-resource");
        dict["canvas-id"].Should().Be($"{NewDdsDomain}/presentation/image1/canvases/canvas1");
        dict["other-dds"].Should().Be($"{NewDdsDomain}/x/y/z");
        
        // expected to stay the same:
        dict["image-service-1"].Should().Be(TestData["image-service-1"]);
        dict["image-service-2"].Should().Be(TestData["image-service-2"]);
        dict["image-params-2"].Should().Be(TestData["image-params-2"]);
        dict["other-wc-link"].Should().Be(TestData["other-wc-link"]);
        dict["other-wc-api-link"].Should().Be(TestData["other-wc-api-link"]);
        dict["other-external-link"].Should().Be(TestData["other-external-link"]);
        dict["video-resource"].Should().Be(TestData["video-resource"]);
        dict["audio-resource"].Should().Be(TestData["audio-resource"]);
        dict["file-resource"].Should().Be(TestData["file-resource"]);
        dict["pdf-derivative"].Should().Be(TestData["pdf-derivative"]);
        dict["auth-service"].Should().Be(TestData["auth-service"]);
        dict["auth-logout"].Should().Be(TestData["auth-logout"]);
        dict["thumb-service-1"].Should().Be(TestData["thumb-service-1"]);
        dict["thumb-params-1"].Should().Be(TestData["thumb-params-1"]);
    }

    [Fact]
    public static void DLCS_Only_Links_rewritten()
    {
        var dlcsOnlyOptions = new DdsOptions
        {
            LinkedDataDomain = InitialDomain,
            RewriteDlcsLinksHostTo = NewDlcsDomain,
            RewriteDlcsLinksSpaceTo = NewDlcsSpace
        };
        var dlcsOnlyLinkRewriter = new LinkRewriter(Options.Create(dlcsOnlyOptions));
        
        // act
        var rewritten = dlcsOnlyLinkRewriter.RewriteLinks(TestDataString);
        
        // assert
        dlcsOnlyLinkRewriter.RequiresRewriting().Should().BeTrue();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(rewritten);
        
        // expected to be transformed
        dict["image-service-1"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image1.jp2");
        dict["image-service-2"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image2.jp2");
        dict["image-params-1"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image1.jp2/full/max/0/default.jpg");
        dict["image-params-2"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image2.jp2/full/max/0/default.jpg");
        dict["video-resource"].Should().Be($"{NewDlcsDomain}/iiif-av/2/6/video.mp4");
        dict["audio-resource"].Should().Be($"{NewDlcsDomain}/iiif-av/2/6/sound.mp3");
        dict["file-resource"].Should().Be($"{NewDlcsDomain}/file/2/6/my-file");
        dict["pdf-derivative"].Should().Be($"{NewDlcsDomain}/pdf/2/pdf/6/my-book");
        dict["auth-service"].Should().Be($"{NewDlcsDomain}/auth/2/clickthrough");
        dict["auth-logout"].Should().Be($"{NewDlcsDomain}/auth/2/clickthrough/logout");
        dict["thumb-service-1"].Should().Be($"{NewDlcsDomain}/thumbs/2/6/image1.jp2");
        dict["thumb-params-1"].Should().Be($"{NewDlcsDomain}/thumbs/2/6/image1.jp2/full/max/0/default.jpg");
        
        // expected to stay the same:
        dict["manifest-id"].Should().Be(TestData["manifest-id"]);
        dict["text-resource"].Should().Be(TestData["text-resource"]);
        dict["alto-resource"].Should().Be(TestData["alto-resource"]);
        dict["canvas-id"].Should().Be(TestData["canvas-id"]);
        dict["other-wc-link"].Should().Be(TestData["other-wc-link"]);
        dict["other-wc-api-link"].Should().Be(TestData["other-wc-api-link"]);
        dict["other-external-link"].Should().Be(TestData["other-external-link"]);
        dict["other-dds"].Should().Be(TestData["other-dds"]);
    }
    
    
    [Fact]
    public static void DLCS_Host_Only_Links_rewritten()
    {
        var dlcsHostOnlyOptions = new DdsOptions
        {
            LinkedDataDomain = InitialDomain,
            RewriteDlcsLinksHostTo = NewDlcsDomain // but no space specified
        };
        var dlcsHostOnlyLinkRewriter = new LinkRewriter(Options.Create(dlcsHostOnlyOptions));
        
        // act
        var rewritten = dlcsHostOnlyLinkRewriter.RewriteLinks(TestDataString);
        
        // assert
        dlcsHostOnlyLinkRewriter.RequiresRewriting().Should().BeTrue();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(rewritten);
        
        // expected to be transformed
        dict["image-service-1"].Should().Be($"{NewDlcsDomain}/image/image1.jp2");
        dict["image-service-2"].Should().Be($"{NewDlcsDomain}/image/image2.jp2");
        dict["image-params-1"].Should().Be($"{NewDlcsDomain}/image/image1.jp2/full/max/0/default.jpg");
        dict["image-params-2"].Should().Be($"{NewDlcsDomain}/image/image2.jp2/full/max/0/default.jpg");
        dict["video-resource"].Should().Be($"{NewDlcsDomain}/av/video.mp4");
        dict["audio-resource"].Should().Be($"{NewDlcsDomain}/av/sound.mp3");
        dict["file-resource"].Should().Be($"{NewDlcsDomain}/file/my-file");
        dict["pdf-derivative"].Should().Be($"{NewDlcsDomain}/pdf/my-book");
        dict["auth-service"].Should().Be($"{NewDlcsDomain}/auth/clickthrough");
        dict["auth-logout"].Should().Be($"{NewDlcsDomain}/auth/clickthrough/logout");
        dict["thumb-service-1"].Should().Be($"{NewDlcsDomain}/thumbs/image1.jp2");
        dict["thumb-params-1"].Should().Be($"{NewDlcsDomain}/thumbs/image1.jp2/full/max/0/default.jpg");
        
        // expected to stay the same:
        dict["manifest-id"].Should().Be(TestData["manifest-id"]);
        dict["text-resource"].Should().Be(TestData["text-resource"]);
        dict["alto-resource"].Should().Be(TestData["alto-resource"]);
        dict["canvas-id"].Should().Be(TestData["canvas-id"]);
        dict["other-wc-link"].Should().Be(TestData["other-wc-link"]);
        dict["other-wc-api-link"].Should().Be(TestData["other-wc-api-link"]);
        dict["other-external-link"].Should().Be(TestData["other-external-link"]);
        dict["other-dds"].Should().Be(TestData["other-dds"]);
    }

    [Fact]
    public static void DDS_And_DLCS_Links_Rewritten()
    {
        var ddsAndDlcsOptions = new DdsOptions
        {
            LinkedDataDomain = InitialDomain,
            RewriteDomainLinksTo = NewDdsDomain,
            RewriteDlcsLinksHostTo = NewDlcsDomain,
            RewriteDlcsLinksSpaceTo = NewDlcsSpace
        };
        var ddsAndDlcsLinkRewriter = new LinkRewriter(Options.Create(ddsAndDlcsOptions));
        // act
        var rewritten = ddsAndDlcsLinkRewriter.RewriteLinks(TestDataString);
        
        // assert
        ddsAndDlcsLinkRewriter.RequiresRewriting().Should().BeTrue();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(rewritten);
        
        // expected to be transformed
        dict["image-service-1"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image1.jp2");
        dict["image-service-2"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image2.jp2");
        dict["image-params-1"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image1.jp2/full/max/0/default.jpg");
        dict["image-params-2"].Should().Be($"{NewDlcsDomain}/iiif-img/2/6/image2.jp2/full/max/0/default.jpg");
        dict["video-resource"].Should().Be($"{NewDlcsDomain}/iiif-av/2/6/video.mp4");
        dict["audio-resource"].Should().Be($"{NewDlcsDomain}/iiif-av/2/6/sound.mp3");
        dict["file-resource"].Should().Be($"{NewDlcsDomain}/file/2/6/my-file");
        dict["pdf-derivative"].Should().Be($"{NewDlcsDomain}/pdf/2/pdf/6/my-book");
        dict["auth-service"].Should().Be($"{NewDlcsDomain}/auth/2/clickthrough");
        dict["auth-logout"].Should().Be($"{NewDlcsDomain}/auth/2/clickthrough/logout");
        dict["thumb-service-1"].Should().Be($"{NewDlcsDomain}/thumbs/2/6/image1.jp2");
        dict["thumb-params-1"].Should().Be($"{NewDlcsDomain}/thumbs/2/6/image1.jp2/full/max/0/default.jpg");
        
        dict["other-dds"].Should().Be($"{NewDdsDomain}/x/y/z");
        dict["manifest-id"].Should().Be($"{NewDdsDomain}/presentation/manifest");
        dict["text-resource"].Should().Be($"{NewDdsDomain}/text/v1/text-resource");
        dict["alto-resource"].Should().Be($"{NewDdsDomain}/text/alto/alto-resource");
        dict["canvas-id"].Should().Be($"{NewDdsDomain}/presentation/image1/canvases/canvas1");
        
        // expected to stay the same:
        dict["other-wc-link"].Should().Be(TestData["other-wc-link"]);
        dict["other-wc-api-link"].Should().Be(TestData["other-wc-api-link"]);
        dict["other-external-link"].Should().Be(TestData["other-external-link"]);
    }

    [Fact]
    public static void NoOp_Links_Not_Rewritten()
    {
        var noopOptions = new DdsOptions
        {
            LinkedDataDomain = InitialDomain
        };
        var noopDlcsLinkRewriter = new LinkRewriter(Options.Create(noopOptions));
        
        // act
        var rewritten = noopDlcsLinkRewriter.RewriteLinks(TestDataString);
        
        // assert
        noopDlcsLinkRewriter.RequiresRewriting().Should().BeFalse();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(rewritten);
        dict.Should().BeEquivalentTo(TestData);
    }
}