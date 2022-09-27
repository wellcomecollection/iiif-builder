using System.Text;
using Utils;

namespace Wellcome.Dds.AssetDomain.Mets;

public class MediaDimensions
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? Duration { get; set; }
    public string DurationDisplay { get; set; }

    public override string ToString()
    {
        bool hasDimensions = false;
        var s = new StringBuilder();
        if (Width is > 0)
        {
            s.Append(Width);
            s.Append(" x ");
            s.Append(Height);
            hasDimensions = true;
        }

        if (Duration.GetValueOrDefault() > 0)
        {
            if (s.Length > 0) s.Append(", ");
            s.Append(Duration);
            s.Append(" (" + DurationDisplay + ")");
            hasDimensions = true;
        }

        if(hasDimensions) return s.ToString();
        return "(dimensionless)";
    }
}