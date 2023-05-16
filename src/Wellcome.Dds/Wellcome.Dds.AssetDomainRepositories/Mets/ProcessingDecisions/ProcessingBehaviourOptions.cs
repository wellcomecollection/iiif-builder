using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviourOptions
{
    public bool UseNamedAVDefaults { get; set; } = false;
    public bool AddThumbsAsSeparateChannel { get; set; } = false;
    public int MaxUntranscodedAccessMp4 { get; set; } = 720;
    public bool MakeAllAccessMP4sAvailable { get; set; } = true;


    /// <summary>
    /// Image formats (contentType: image/*) that ARE NOT in this list will be `file` only
    /// </summary>
    public Dictionary<string, string[]> ImageDeliveryChannels { get; set; } = new()
    {
        ["jp2"]  = new[] { Channels.IIIFImage },  // do not serve JP2s on the file channel, yet
        ["jpg"]  = new[] { Channels.IIIFImage, Channels.File },
        ["jpeg"] = new[] { Channels.IIIFImage, Channels.File },
        ["tif"]  = new[] { Channels.IIIFImage, Channels.File },
        ["tiff"] = new[] { Channels.IIIFImage, Channels.File },
        ["png"]  = new[] { Channels.IIIFImage, Channels.File },
        ["gif"]  = new[] { Channels.IIIFImage, Channels.File },
        ["bmp"]  = new[] { Channels.IIIFImage, Channels.File }
    };

    public string[] DefaultImageDeliveryChannels { get; set; } = { Channels.File };

}