namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviourOptions
{
    public bool UseNamedAVDefaults { get; set; } = false;
    public bool MakeAllSourceImagesAvailable { get; set; } = false;
    public bool MakeJP2Available { get; set; } = false;
    public bool AddThumbsAsSeparateChannel { get; set; } = false;
    public int MaxUntranscodedAccessMp4 { get; set; } = 720;
    public bool MakeAllAccessMP4sAvailable { get; set; } = true;

    /// <summary>
    /// Even if an asset has a content type of image/xxx, it will be treated as a file
    /// unless xxx is in this list
    /// </summary>
    public string[] ImageServiceFormats { get; set; } = 
        { "jp2", "j2k", "jpg", "jpeg", "tif", "tiff", "png", "gif", "bmp" };
}