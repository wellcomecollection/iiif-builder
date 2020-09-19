using System.Collections.Generic;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    public class MultiCopyState
    {
        public Dictionary<string, CopyAndVolume> CopyAndVolumes = new Dictionary<string, CopyAndVolume>();
    }

    public class CopyAndVolume
    {
        public string Id { get; set; }
        public int CopyNumber { get; set; }
        public int VolumeNumber { get; set; }
    }
}