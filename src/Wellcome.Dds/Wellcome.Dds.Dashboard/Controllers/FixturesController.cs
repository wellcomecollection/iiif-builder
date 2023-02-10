using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Dashboard.Models.Fixtures;

namespace Wellcome.Dds.Dashboard.Controllers;

public class FixturesController : Controller
{
    private readonly StorageOptions storageOptions;
    private readonly IWebHostEnvironment environment;
    
    public FixturesController(IOptions<StorageOptions> options, IWebHostEnvironment environment)
    {
        storageOptions = options.Value;
        this.environment = environment;
    }

    public ActionResult Index()
    {
        return View("FixturesList");
    }

    public ActionResult Digitised()
    {
        return View("Fixtures", GetDigitised());
    }

    public ActionResult BornDigitalSheet()
    {
        // Use this to get JSON for Ashley's sheet: https://tableconvert.com/excel-to-json
        var rawDataPath = System.IO.Path.Combine(environment.WebRootPath, "fixtures", "bd-staging.json");
        var rawData = System.IO.File.ReadAllText(rawDataPath);
        var rawDict = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(rawData);
        return View("SheetFixtures", rawDict);
    }
    
    public ActionResult BornDigital()
    {
        return View("Fixtures", GetBornDigital());
    }

    private (string, string)[] GetDigitised()
    {
        return new DigitisedProduction().Identifiers;
    }

    private (string, string)[] GetBornDigital()
    {
        string[] identifiers = storageOptions.StorageApiTemplate.Contains("api-stage")
            ? new BornDigitalStaging().Identifiers
            : new BornDigitalProduction().Identifiers;
        return identifiers.Select(identifier => new ValueTuple<string, string>(identifier, string.Empty)).ToArray();
    }
}