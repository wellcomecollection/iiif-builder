using Wellcome.Dds.AssetDomain.DigitalObjects;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public static class ChannelNames
{
    public const string IIIFImage = "iiif-img";
    public const string IIIFAv = "iiif-av";
    public const string Thumbs = "thumbs";
    public const string File = "file";
}

public static class Channels
{
    public static DeliveryChannel IIIFImage(string? policy = null)
    {
        return new DeliveryChannel(ChannelNames.IIIFImage, policy);
    }
    public static DeliveryChannel IIIFAv(string? policy = null)
    {
        return new DeliveryChannel(ChannelNames.IIIFAv, policy);
    }
    public static DeliveryChannel Thumbs(string? policy = null)
    {
        return new DeliveryChannel(ChannelNames.Thumbs, policy);
    }
    public static DeliveryChannel File(string? policy = null)
    {
        return new DeliveryChannel(ChannelNames.File, policy);
    }
    
}