using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models.Fixtures;

namespace Wellcome.Dds.Dashboard.Controllers;

public class FixturesController : Controller
{
    private StorageOptions storageOptions;
    
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

    private IEnumerable<DdsIdentifier> GetDigitised()
    {
        if (storageOptions.StorageApiTemplate.Contains("api-stage"))
        {
            return new DigitisedStaging().Identifiers.Select(id => new DdsIdentifier(id));
        }
        return new DigitisedProduction().Identifiers.Select(id => new DdsIdentifier(id));
    }

    private IEnumerable<DdsIdentifier> GetBornDigital()
    {
        if (storageOptions.StorageApiTemplate.Contains("api-stage"))
        {
            return new BornDigitalStaging().Identifiers.Select(id => new DdsIdentifier(id));
        }
        return new BornDigitalStaging().Identifiers.Select(id => new DdsIdentifier(id));
    }
}