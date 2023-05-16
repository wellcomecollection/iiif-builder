
// Usage:

// Run the default list of fixtures:
// IIIFServerSmokeTest.exe 

// Run the default list of fixtures, expecting them to be more recent than 2023-05-01:
// IIIFServerSmokeTest.exe 2023-05-01

// Run a particular identifier using only the information from the initial Manifest load:
// IIIFServerSmokeTest.exe b12121212

// Run a particular identifier using only the information from the initial Manifest load, expecting Manifest to be later than 2023-05-01
// IIIFServerSmokeTest.exe 2023-05-01 b12121212

// Run multiple identifiers, just following links from first load
// IIIFServerSmokeTest.exe b12121212 b23232323 b34343434
// IIIFServerSmokeTest.exe 2023-05-01 b12121212 b23232323 b34343434



using IIIFServerSmokeTest;

DateTime minDateTime = DateTime.MinValue;
List<WorkFixture> fixtures;

if (args.Length == 0)
{
    fixtures = new Fixtures(minDateTime).List;
}
else
{
    int argIndex = 0;
    if (DateTime.TryParse(args[0], out minDateTime))
    {
        argIndex = 1;
    }

    if (args.Length > argIndex + 1)
    {
        fixtures = new List<WorkFixture>();
        for (int i = argIndex; i < args.Length; i++)
        {
            fixtures.Add(new WorkFixture(args[i], args[i]));
        }
    }
    else
    {
        fixtures = new Fixtures(minDateTime).List;
    }
}

var smokeTester = new SmokeTester(
    "https://iiif.wellcomecollection.org", // This needs to be a command line arg
    "https://api.wellcomecollection.org",
    "https://api.wellcomecollection.org/catalogue/v2/works");

int fixtureCount = 0;
int failureCount = 0;

// helps with adding new ones;
fixtures.Reverse();

foreach (var fixture in fixtures)
{
    var result = await smokeTester.Test(fixture);
    var outcome = result.Success ? "success" : "FAILURE";
    Console.WriteLine($"========== {outcome}");
    Console.WriteLine();
    fixtureCount++;
    if (!result.Success)
    {
        failureCount++;
    }
}

Console.WriteLine();
Console.WriteLine($"{failureCount} failure(s) from {fixtureCount} fixtures.");
Console.WriteLine();

