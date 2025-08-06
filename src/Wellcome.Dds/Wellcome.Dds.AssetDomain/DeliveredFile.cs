namespace Wellcome.Dds.AssetDomain;

/// <summary>
/// Represents files and derivatives of files served by DLCS
/// </summary>
public class DeliveredFile
{
    public string? PublicUrl { get; set; }
    public string? DlcsUrl { get; set; }
    public string? DeliveryChannel { get; set; } // always single, represents an OUTPUT
    
    public string? MediaType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? Duration { get; set; }

    public string GetSummary()
    {
        var s = MediaType;
        if (Duration.HasValue)
        {
            s += $"\nDuration: {Duration}";
        }

        if (Height.HasValue)
        {
            s += $"\nWidth: {Width}, Height: {Height}";
        }

        return s ?? "(no summary information)";
    }
}