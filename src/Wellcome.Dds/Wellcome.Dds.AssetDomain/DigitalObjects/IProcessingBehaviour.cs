using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.DigitalObjects;

public interface IProcessingBehaviour
{
    HashSet<string> DeliveryChannels { get; }
    string? ImageOptimisationPolicy { get; }
}