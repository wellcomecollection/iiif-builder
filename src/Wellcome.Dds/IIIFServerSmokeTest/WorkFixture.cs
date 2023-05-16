using Wellcome.Dds.Common;

namespace IIIFServerSmokeTest;

public class WorkFixture
{
    public WorkFixture(string identifier, string label)
    {
        Identifier = new DdsIdentifier(identifier);
        Label = label;
    }
    
    public DdsIdentifier Identifier { get; }
    
    public string Label { get; }
    
    public DateTime? ManifestShouldBeAfter { get; set; }

    public bool? IdentifierIsCollection { get; set; }
    
    // Only for collections
    public int? ManifestCount { get; set; } = 0;
    
    public bool? HasAlto { get; set; }
    
    public bool? HasTranscriptAsDocument { get; set; }

    public bool Skip { get; set; } = false;


}