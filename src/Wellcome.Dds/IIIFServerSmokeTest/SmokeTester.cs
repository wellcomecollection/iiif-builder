using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace IIIFServerSmokeTest;

public class SmokeTester
{
    private UriPatterns uriPatterns;

    public SmokeTester(string publicIIIFRoot, string wellcomeCollectionApi, string apiWorkTemplate)
    {
        var options = Options.Create(new DdsOptions
        {
            LinkedDataDomain = publicIIIFRoot,
            WellcomeCollectionApi = wellcomeCollectionApi,
            ApiWorkTemplate = apiWorkTemplate
        });
        uriPatterns = new UriPatterns(options);
    }

    public async Task<Result> Test(WorkFixture fixture)
    {
        var result = new Result();
        result.Add($"Testing fixture {fixture.Identifier}: {fixture.Label}");
        var client = new HttpClient();
        var initialIIIF = uriPatterns.Manifest(fixture.Identifier);
        var jsonString = await client.GetStringAsync(initialIIIF);
        try
        {
            var iiifObject = JObject.Parse(jsonString);
            var type = iiifObject["type"]!.Value<string>();
            if (fixture.IdentifierIsCollection.HasValue)
            {
                if (fixture.IdentifierIsCollection.Value && type == "Collection")
                {
                    result.Add($"Parsed an expected Collection from {initialIIIF}");
                } 
                else if(!fixture.IdentifierIsCollection.Value && type == "Manifest")
                {
                    result.Add($"Parsed an expected Manifest from {initialIIIF}");
                }
                else
                {
                    result.AddFailure($"Unexpected type {type} from {initialIIIF}");
                }
            }
            else
            {
                if (type is "Collection" or "Manifest")
                {
                    result.Add($"Determined {initialIIIF} is a {type}");
                }
                else
                {
                    result.AddFailure($"Unexpected type {type} from {initialIIIF}");
                }
            }

            if (type == "Collection")
            {
                IList<JToken> manifests = iiifObject["items"]!.Children().ToList();
                if (fixture.ManifestCount.HasValue)
                {
                    if (manifests.Count == fixture.ManifestCount.Value)
                    {
                        result.Add($"Collection has the expected {fixture.ManifestCount.Value} items.");
                    }
                    else
                    {
                        result.AddFailure($"Collection DOES NOT have the expected {fixture.ManifestCount.Value} items.");
                    }
                }
                else
                {
                    result.Add($"Collection observed to have {manifests.Count} items.");
                }
                foreach (var manifest in manifests)
                {
                    var manifestUrl = manifest["id"]!.Value<string>();
                    JObject? manifestObject = null;
                    try
                    {
                        var manifestJson = await client.GetStringAsync(manifestUrl);
                        manifestObject = JObject.Parse(manifestJson);
                    }
                    catch(Exception mex)
                    {
                        result.AddFailure("Unable to load and parse " + manifestUrl);
                        result.AddFailure(mex.Message);
                    }

                    if (manifestObject != null)
                    {
                        await TestManifest(client, fixture, manifestObject, result);
                    }
                }
            }
            else if (type == "Manifest")
            {
                await TestManifest(client, fixture, iiifObject, result);
            }
            else
            {
                result.AddFailure($"Unknown type: {type}");
            }
        }
        catch (Exception ex)
        {
            result.AddFailure("Unable to load and parse " + initialIIIF);
            result.AddFailure(ex.Message);
        }

        return result;
    }

    private async Task TestManifest(HttpClient client, WorkFixture fixture, JObject manifest, Result result)
    {
        var manifestId = manifest["id"]!.Value<string>();
        result.Add($"Testing manifest {manifestId}");
        
        IList<JToken> services = manifest["services"]!.Children().ToList();
        CheckTimestamp(fixture, result, services);

        IList<JToken> service = manifest["service"]!.Children().ToList();
        await TestSearchService(client, fixture, result, service);
        
        // we don't want to test ALL the canvases. If there are more than three, test the first, middle and last canvases.
        IList<JToken> canvases = manifest["items"]!.Children().ToList();
        TestCanvas(canvases[0], client, fixture, result);
        switch (canvases.Count)
        {
            case 1:
                break; // already done
            case 2:
                TestCanvas(canvases[1], client, fixture, result);
                break;
            case 3:
                TestCanvas(canvases[1], client, fixture, result);
                TestCanvas(canvases[2], client, fixture, result);
                break;
            default:
                TestCanvas(canvases[canvases.Count / 2], client, fixture, result);
                TestCanvas(canvases[^1], client, fixture, result);
                break;
        }
    }

    private void TestCanvas(JToken canvas, HttpClient client, WorkFixture fixture, Result result)
    {
        var canvasId = canvas["id"]!.Value<string>();
        result.Add($"Testing canvas {canvasId}");
    }

    private static async Task TestSearchService(HttpClient client, WorkFixture fixture, Result result, IList<JToken> service)
    {
        var searchService = service.SingleOrDefault(s => s["@type"]!.Value<string>() == "SearchService1");
        if (fixture.HasAlto.HasValue)
        {
            if (fixture.HasAlto.Value && searchService != null)
            {
                result.Add($"Expected Search Service found");
            }
            else if (fixture.HasAlto.Value && searchService == null)
            {
                result.AddFailure("Did not find expected search service");
            }
            else if (!fixture.HasAlto.Value && searchService == null)
            {
                result.Add($"Not expected to have Search Service and none found");
            }
            else if (!fixture.HasAlto.Value && searchService != null)
            {
                result.AddFailure("Did not expect a search service but found one");
            }
        }

        if (searchService != null)
        {
            try
            {
                // we'll test it anyway regardless of whether it was expected
                var searchServiceQuery = searchService["@id"]!.Value<string>() + "?q=xyz";
                var searchResponse = await client.GetStringAsync(searchServiceQuery);
                var searchResponseObj = JObject.Parse(searchResponse);
                if (searchResponseObj.ContainsKey("hits"))
                {
                    // it's OK (and likely) for hits to be empty
                    result.Add($"Search service returned result with hits property (from {searchServiceQuery})");
                }
                else
                {
                    result.AddFailure($"No hits property on search response to {searchServiceQuery}");
                }

                // Now for autocomplete - always the immediate child service
                var autocompleteQuery = searchService["service"]!["@id"]!.Value<string>() + "?q=xyz";
                var autocompleteResponse = await client.GetStringAsync(autocompleteQuery);
                var autocompleteResponseObj = JObject.Parse(autocompleteResponse);
                if (autocompleteResponseObj.ContainsKey("terms"))
                {
                    // it's OK (and likely) for terms to be empty
                    result.Add($"Autocomplete service returned result with terms property (from {autocompleteQuery})");
                }
                else
                {
                    result.AddFailure($"No terms property on Autocomplete response to {autocompleteQuery}");
                }
            }
            catch (Exception ex)
            {
                result.AddFailure($"Unable to use search service: " + ex.Message);
            }
        }
    }

    private static void CheckTimestamp(WorkFixture fixture, Result result, IList<JToken> services)
    {
        const string timestampProfile = "https://github.com/wellcomecollection/iiif-builder/build-timestamp";
        var timestampService = services.Single(s => s["profile"]!.Value<string>() == timestampProfile);
        var timestamp = timestampService["label"]!["none"]![0]!.Value<string>();
        var timestampDt = DateTime.Parse(timestamp);
        if (timestampDt > fixture.ManifestShouldBeAfter)
        {
            result.Add("Manifest generated after cutoff: " + timestamp);
        }
        else
        {
            result.AddFailure("Manifest is OLDER than cutoff: " + timestamp);
        }
    }


    /// <summary>
    /// This isn't working yet
    /// </summary>
    /// <param name="fixture"></param>
    /// <returns></returns>
    public async Task<string[]> TestWithIIIFNet(WorkFixture fixture)
    {
        var messages = new List<string>();
        var client = new HttpClient();
        var initialIIIF = uriPatterns.Manifest(fixture.Identifier);
        var jsonString = await client.GetStringAsync(initialIIIF);
        Manifest? manifest = null;
        Collection? collection = null;
        try
        {

            if (fixture.IdentifierIsCollection.HasValue)
            {
                // explicitly stated to be a collection or manifest
                if (fixture.IdentifierIsCollection.Value)
                {
                    collection = jsonString.FromJson<Collection>();
                }
                else
                {
                    manifest = jsonString.FromJson<Manifest>();
                }
            }
            else
            {
                // we'll need to work out what it is
                var jo = JObject.Parse(jsonString);
                var jType = jo["type"]!.Value<string>();
                if (jType == "Collection")
                {
                    collection = jsonString.FromJson<Collection>();
                }
                else if (jType == "Manifest")
                {
                    manifest = jsonString.FromJson<Manifest>();
                }
                else
                {
                    messages.Add("Unknown type: " + jType);
                }
            }
        }
        catch (Exception ex)
        {
            messages.Add("Unable to load and parse " + initialIIIF);
            messages.Add(ex.Message);
            return messages.ToArray();
        }

        if (collection == null && manifest == null)
        {
            messages.Add("Unable to load and parse " + initialIIIF);
            return messages.ToArray();
        }

        if (collection != null)
        {
            Console.WriteLine("Loaded Collection from " + initialIIIF);
        } 


        if (manifest != null)
        {
            Console.WriteLine("Loaded Manifest from " + initialIIIF);
        } 
        
        return messages.ToArray();
    }
}