using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
        JObject? iiifObject = null;
        try
        {
            iiifObject = JObject.Parse(jsonString);
            if (iiifObject.ContainsKey("type"))
            {
                var type = iiifObject["type"].Value<string>();
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
                    IList<JToken> manifests = iiifObject["items"].Children().ToList();
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
                        var manifestUrl = manifest["id"].Value<string>();
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
                            TestManifest(fixture, manifestObject, result);
                        }
                    }
                }
                else if (type == "Manifest")
                {
                    TestManifest(fixture, iiifObject, result);
                }
                else
                {
                    result.AddFailure($"Unknown type: {type}");
                }
            }
        }
        catch (Exception ex)
        {
            result.AddFailure("Unable to load and parse " + initialIIIF);
            result.AddFailure(ex.Message);
        }

        return result;
    }

    private void TestManifest(WorkFixture fixture, JObject manifest, Result result)
    {
        result.Add("Testing manifest");
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
                var jType = jo["type"].Value<string>();
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