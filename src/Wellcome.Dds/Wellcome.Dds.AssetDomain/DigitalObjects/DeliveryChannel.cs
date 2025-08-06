namespace Wellcome.Dds.AssetDomain.DigitalObjects;

public class DeliveryChannel
{
    public DeliveryChannel()
    {
    }

    public DeliveryChannel(string channel)
    {
        Channel = channel;
    }
    
    public DeliveryChannel(string channel, string? policy)
    {
        Channel = channel;
        Policy = policy;
    }

    public string? Channel { get; set; }
    
    public string? Policy { get; set; }

    public override int GetHashCode()
    {
        return $"{Channel}|{Policy}".GetHashCode();
    }
}