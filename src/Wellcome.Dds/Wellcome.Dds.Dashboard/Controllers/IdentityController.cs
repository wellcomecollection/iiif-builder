using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers;

/// <summary>
/// The DDS's own identity records (the identities table). Not to be confused with
/// <see cref="IdentifiersController"/>, which queries the catalogue's Identifiers API.
/// </summary>
public class IdentityController : Controller
{
    private readonly IIdentityService identityService;
    private readonly IIdentityStatsRepository statsRepository;

    public IdentityController(IIdentityService identityService, IIdentityStatsRepository statsRepository)
    {
        this.identityService = identityService;
        this.statsRepository = statsRepository;
    }

    /// <summary>
    /// Counts over the identities table: how many, of what kind, from where, how much is validated.
    /// </summary>
    public async Task<ActionResult> Stats()
    {
        return View(await statsRepository.GetStats());
    }


    public ActionResult Get(string id)
    {
        try
        {
            var ddsId = identityService.GetIdentity(id);
            return View("GetIdentity", ddsId);
        }
        catch (FormatException ex)
        {
            return NotFound($"'{id}' is not a valid identifier. {ex.Message}");
        }
    }

}