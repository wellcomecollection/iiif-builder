using System.Collections.Generic;
using IIIF;

namespace Wellcome.Dds.AssetDomain.DigitalObjects;

public interface IProcessingBehaviour
{
    HashSet<string> DeliveryChannels { get; }
    string? ImageOptimisationPolicy { get; }
    Size? GetVideoSize(string deliveryChannel);
}