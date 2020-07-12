using System;

namespace Wellcome.Dds.Data
{
    /// <summary>
    /// This is equivalent to FlatManifestation in old DDS.
    /// It's almost a IIIF Manifest row, but not quite.
    /// Still some rework to do before it can become "Manifest"
    /// </summary>
    public class Manifestation
    {
        public string Id { get; set; }
        public string PackageIdentifier { get; set; }
        public string ManifestationIdentifier { get; set; }
        public DateTime Processed { get; set; }
        public int PackageShortBNumber { get; set; }
        public string Label { get; set; }
    }
}
