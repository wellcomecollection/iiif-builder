using System.Collections.Generic;
using IIIF;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.AssetDomain.DigitalObjects;

public interface IProcessingBehaviour
{
    HashSet<string> DeliveryChannels { get; }
    string? ImageOptimisationPolicy { get; }
    Size? GetVideoSize(string deliveryChannel);
    AssetFamily AssetFamily { get; }
}