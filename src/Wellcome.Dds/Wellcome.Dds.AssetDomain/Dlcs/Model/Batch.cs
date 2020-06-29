using System;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class Batch : JSONLDBase
    {
        public int Count { get; set; }
        public int Completed { get; set; }
        public int Errors { get; set; }
        public DateTime? Submitted { get; set; }
        public DateTime? Finished { get; set; }
        public bool Superseded { get; set; }
    }
}
