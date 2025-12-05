using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers;

public class IdentityController : Controller
{
    private readonly IIdentityService identityService;

    public IdentityController(IIdentityService identityService)
    {
        this.identityService = identityService;
    }
    
    
    public ActionResult Get(string id)
    {
        var ddsId = identityService.GetIdentity(id);
        return View("GetIdentity", ddsId);
    }
    
}