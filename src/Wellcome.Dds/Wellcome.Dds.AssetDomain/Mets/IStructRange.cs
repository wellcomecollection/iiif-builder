using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{    // A bridge from METS to IIIF
    public interface IStructRange
    {
        string Label { get; set; }
        string Type { get; set; }
        string Id { get; set; }
        List<string> PhysicalFileIds { get; set; }
        List<IStructRange> Children { get; set; }
        IModsData Mods { get; set; } // TODO: Why was this not a good idea before?
    }
}
