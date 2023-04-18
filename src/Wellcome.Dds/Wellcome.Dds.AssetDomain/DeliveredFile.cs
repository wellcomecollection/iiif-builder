namespace Wellcome.Dds.AssetDomain;

/// <summary>
/// Represents files and derivatives of files served by DLCS
/// </summary>
public class DeliveredFile
{
    public string? PublicUrl { get; set; }
    public string? DlcsUrl { get; set; }
    public string? DeliveryChannel { get; set; }
    
    public string? MediaType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
}