using IIIF;
using IIIF.Presentation.V3.Content;

namespace Wellcome.Dds.Repositories.Presentation;

public class SimpleTextBasedService : ExternalResource, IService
{
    public SimpleTextBasedService() : base("Text")
    {
        
    }
}