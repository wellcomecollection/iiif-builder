namespace IIIFServerSmokeTest;

public class Fixtures
{
    public List<WorkFixture> List { get; }

    public Fixtures(DateTime minimumDateTime)
    {
        List = new List<WorkFixture>()
        {
            new("b2178081x", "2 smallish volumes")
            {
                IdentifierIsCollection = true,
                ManifestCount = 2,
                HasAlto = true
            },
            new("b30136155", "short MOH report")
            {
                IdentifierIsCollection = false,
                HasAlto = true
            }
        };
        
        foreach (var fixture in List)
        {
            fixture.ManifestShouldBeAfter = minimumDateTime;
        }
    }
}