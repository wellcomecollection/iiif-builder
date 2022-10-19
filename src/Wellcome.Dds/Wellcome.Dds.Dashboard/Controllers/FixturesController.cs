using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Dashboard.Models.Fixtures;

namespace Wellcome.Dds.Dashboard.Controllers;

public class FixturesController : Controller
{
    private readonly StorageOptions storageOptions;
    
    public FixturesController(IOptions<StorageOptions> options)
    {
        storageOptions = options.Value;
    }
    
    public ActionResult Digitised()
    {
        return View("Fixtures", GetDigitised());
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