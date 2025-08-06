using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.DigitalObjects;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviourOptions
{
    public bool UseNamedAVDefaults { get; set; } = false;
    public int MaxUntranscodedAccessMp4 { get; set; } = 720;
    public bool MakeAllAccessMP4sAvailable { get; set; } = true;

    
    /// <summary>
    /// Image formats (contentType: image/*) that ARE NOT in this list will be `file` only
    /// The thumbs channel is added later
    /// </summary>
    public Dictionary<string, DeliveryChannel[]> ImageDeliveryChannels { get; set; } = new()
    {
        ["jp2"]  = new[] { Channels.IIIFImage("use-original"), Channels.Thumbs() },  // do not serve JP2s on the file channel, yet
        ["jpg"]  = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["jpeg"] = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["tif"]  = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["tiff"] = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["png"]  = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["gif"]  = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") },
        ["bmp"]  = new[] { Channels.IIIFImage("default"),      Channels.Thumbs(),    Channels.File("none") }
    };
    
    public DeliveryChannel[] DefaultImageDeliveryChannels { get; set; } = { Channels.File("none") };

}