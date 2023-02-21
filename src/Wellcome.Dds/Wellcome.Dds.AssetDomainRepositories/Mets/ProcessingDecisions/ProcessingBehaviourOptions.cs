namespace Wellcome.Dds.AssetDomainRepositories.Mets.ProcessingDecisions;

public class ProcessingBehaviourOptions
{
    public bool UseNamedAVDefaults { get; set; } = false;
    public bool MakeAllSourceImagesAvailable { get; set; } = false;
    public bool MakeJP2Available { get; set; } = true;
    public int MaxUntranscodedAccessMp4 { get; set; } = 720;
    public bool MakeAllAccessMP4sAvailable { get; set; } = true;
}