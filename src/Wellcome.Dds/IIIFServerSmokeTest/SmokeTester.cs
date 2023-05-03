using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
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
        if (fixture.Skip)
        {
            result.Add($"SKIPPING fixture {fixture.Identifier}: {fixture.Label}");
            return result;
        }
        result.Add($"Testing fixture {fixture.Identifier}: {fixture.Label}");
        var client = new HttpClient();
        var initialIIIF = uriPatterns.Manifest(fixture.Identifier);
        try
        {
            var sw = Stopwatch.StartNew();
            var jsonString = await client.GetStringAsync(initialIIIF);
            sw.Stop();
            result.Add($"Loaded {jsonString.Length} chars in {sw.ElapsedMilliseconds} ms from {initialIIIF}");
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
                        result.AddFailure($"Collection DOES NOT have the expected {fixture.ManifestCount.Value} items - it has {manifests.Count}");
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

        await TestSearchService(client, fixture, result, manifest);

        await TestManifestLevelAnnotations(client, fixture, manifest, result);

        // we don't want to test ALL the canvases. If there are more than three, test the first, middle and last canvases.
        IList<JToken> canvases = manifest["items"]!.Children().ToList();
        await TestCanvas(canvases[0], client, fixture, result);
        switch (canvases.Count)
        {
            case 1:
                break; // already done
            case 2:
                await TestCanvas(canvases[1], client, fixture, result);
                break;
            case 3:
                await TestCanvas(canvases[1], client, fixture, result);
                await TestCanvas(canvases[2], client, fixture, result);
                break;
            default:
                await TestCanvas(canvases[canvases.Count / 2], client, fixture, result);
                await TestCanvas(canvases[^1], client, fixture, result);
                break;
        }
    }

    private static async Task TestManifestLevelAnnotations(HttpClient client, WorkFixture fixture, JObject manifest,
        Result result)
    {
        JObject? manifestImageAnnotations = null;
        if (manifest.ContainsKey("annotations"))
        {
            try
            {
                var imagesAndFigures = manifest["annotations"]!.Children().ToList()
                    .Single(ap => ap["id"]!.Value<string>()!.EndsWith("/images"));
                var imagesAndFiguresJson = await client.GetStringAsync(imagesAndFigures["id"]!.Value<string>());
                manifestImageAnnotations = JObject.Parse(imagesAndFiguresJson);
            }
            catch (Exception aex)
            {
                result.AddFailure("Unable to load manifest annotations: " + aex.Message);
            }
        }

        if (fixture.HasAlto.HasValue)
        {
            if (fixture.HasAlto.Value && manifestImageAnnotations != null)
            {
                result.Add($"Expected Manifest-level images and figure annotations found");
            }
            else if (fixture.HasAlto.Value && manifestImageAnnotations == null)
            {
                result.AddFailure("Did not find expected Manifest-level images and figure annotations");
            }
            else if (!fixture.HasAlto.Value && manifestImageAnnotations == null)
            {
                result.Add($"Not expected to have Manifest-level images and figure annotations, and none found");
            }
            else if (!fixture.HasAlto.Value && manifestImageAnnotations != null)
            {
                result.AddFailure("Did not expect Manifest-level images and figure annotations but found some!");
            }
        }

        if (manifestImageAnnotations != null)
        {
            if (manifestImageAnnotations.ContainsKey("items") && manifestImageAnnotations.ContainsKey("type"))
            {
                result.Add("Manifest has image annotations.");
            }
        }
    }

    private async Task TestCanvas(JToken canvas, HttpClient client, WorkFixture fixture, Result result)
    {
        var canvasObj = (JObject)canvas;
        var canvasId = canvas["id"]!.Value<string>();
        result.Add($"Testing canvas {canvasId}");
        
        // TODO - look for expected images, or AV, etc

        XElement? altoXml = null;
        JObject? pageAnnotations = null;

        if (canvasObj.ContainsKey("seeAlso"))
        {
            try
            {
                var altoUrl = canvasObj["seeAlso"]!.Children().Single()["id"]!.Value<string>()!;
                altoXml = XElement.Load(altoUrl);
                result.Add("Loaded alto XML from " + altoUrl);
            }
            catch (Exception xex)
            {
                result.AddFailure("Could not load ALTO XML: " + xex.Message);
            }
        }
        if (canvasObj.ContainsKey("annotations"))
        {
            // this is either an external annopage of text, or a PDF transcript of AV.
            var annoPage = (JObject)canvasObj["annotations"]!.Children().Single();
            if (annoPage.ContainsKey("items"))
            {
                try
                {
                    var docUrl = annoPage["items"]!.Children().ToList()[0]["body"]!["id"]!.Value<string>();
                    var resp = await client.GetAsync(docUrl);
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        if (fixture.HasTranscriptAsDocument.HasValue && fixture.HasTranscriptAsDocument.Value)
                        {
                            result.Add("Found and loaded expected transcript at " + docUrl);
                        }
                        else
                        {
                            result.Add("Found and loaded transcript at " + docUrl);
                        }
                    }
                    else if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // TODO - need to tie this to testing auth.
                        result.Add($"Access to {docUrl} unauthorised");
                    }
                    else
                    {
                        result.AddFailure("Could not load transcript at " + docUrl);
                    }
                }
                catch (Exception ex)
                {
                    result.AddFailure("Unable to load linked transcript");
                }
            }
            else
            {
                try
                {
                    var pageAnnosId = annoPage["id"]!.Value<string>();
                    var pageAnnotationsJson = await client.GetStringAsync(pageAnnosId);
                    pageAnnotations = JObject.Parse(pageAnnotationsJson);
                    result.Add("Loaded page annotations from " + pageAnnosId);
                }
                catch (Exception aex)
                {
                    result.AddFailure("Could not load Page annotations: " + aex.Message);
                }
            }
        }
        // for now check the text endpoints
        if (fixture.HasAlto.HasValue)
        {
            if (fixture.HasAlto.Value && altoXml != null && pageAnnotations != null)
            {
                result.Add($"Expected ALTO XML and Page Annotations found");
            }
            else if (fixture.HasAlto.Value && (altoXml == null || pageAnnotations == null))
            {
                result.AddFailure("Did not find expected ALTO XML and Page Annotations");
            }
            else if (!fixture.HasAlto.Value && altoXml == null && pageAnnotations == null)
            {
                result.Add($"Not expected to have ALTO XML and Page Annotations and none found");
            }
            else if (!fixture.HasAlto.Value && (altoXml != null || pageAnnotations != null))
            {
                result.AddFailure("Did not expect ALTO XML and Page Annotations but found some");
            }
        }
    }

    private static async Task TestSearchService(HttpClient client, WorkFixture fixture, Result result, JObject manifest)
    {
        JToken? searchService = null;
        if (manifest.ContainsKey("service"))
        {
            IList<JToken> service = manifest["service"]!.Children().ToList();
            searchService = service.SingleOrDefault(s => s["@type"]!.Value<string>() == "SearchService1");
        }
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
        var timestampDt = timestampService["label"]!["none"]![0]!.Value<DateTime>();
        if (timestampDt > fixture.ManifestShouldBeAfter)
        {
            result.Add("Manifest generated after cutoff: " + timestampDt);
        }
        else
        {
            result.AddFailure("Manifest is OLDER than cutoff: " + timestampDt);
        }
    }


    /// <summary>
    /// This isn't working yet - needs improvements to iiif-net deserialization.
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