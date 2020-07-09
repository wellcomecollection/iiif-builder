using System;

namespace Wellcome.Dds.Common
{
    /// <summary>
    /// An identifier associated with a date, for sorting purposes
    /// </summary>
    public class DatedIdentifier
    {
        public string Identifier { get; set; }
        public string Label { get; set; }
        public DateTime Date { get; set; }

        public override string ToString()
        {
            return string.Format("[DatedIdentifier] {0} {1:s} {2}", Identifier, Date, Label);
        }
    }
}
