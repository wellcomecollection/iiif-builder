using System;

namespace Wellcome.Dds
{
    [Obsolete("Needs Attention", false)]
    public class CacheBustResult
    {
        public string type { get; set; }
        public DateTime issued { get; set; }
        public string identifier { get; set; }
        public int manifestation { get; set; }
        public string status { get; set; }

        public override string ToString()
        {
            return string.Format("{0}/{1} - {2}: {3} - {4}",
                identifier,
                manifestation,
                type,
                issued,
                status);
        }
    }
}
