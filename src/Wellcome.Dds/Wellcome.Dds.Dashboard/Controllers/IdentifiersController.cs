using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Dashboard.Controllers;

/// <summary>
/// Queries the catalogue's Identifiers API (RFC 089). Not to be confused with <see cref="IdentityController"/>,
/// which looks up the DDS's own identity records.
/// </summary>
[Route("Identifiers")]
public class IdentifiersController : Controller
{
    private readonly IIdentifiersApi identifiersApi;
    private readonly IdentifiersApiOptions options;

    public IdentifiersController(
        IIdentifiersApi identifiersApi,
        IOptions<IdentifiersApiOptions> options)
    {
        this.identifiersApi = identifiersApi;
        this.options = options.Value;
    }

    /// <summary>
    /// The query form, and the result of an explicit forward or reverse lookup.
    /// </summary>
    [HttpGet("")]
    public async Task<ActionResult> Index(
        string canonicalId = null,
        string sourceSystem = null,
        string value = null,
        string type = null,
        bool siblings = false)
    {
        var model = NewModel();
        model.CanonicalId = canonicalId?.Trim();
        model.SourceSystem = sourceSystem?.Trim();
        model.Value = value?.Trim();
        model.Type = type.HasText() ? type.Trim() : OntologyType.Work;
        model.IncludeSiblings = siblings;

        try
        {
            if (model.CanonicalId.HasText())
            {
                model.ApiUrl = identifiersApi.GetApiUrl(model.CanonicalId);
                model.IdentifierSet = await identifiersApi.GetByCanonicalId(model.CanonicalId);
                model.NotFound = model.IdentifierSet == null;
            }
            else if (model.SourceSystem.HasText() && model.Value.HasText())
            {
                model.ApiUrl = identifiersApi.GetApiUrl(model.SourceSystem, model.Value, model.Type, model.IncludeSiblings);
                if (model.IncludeSiblings)
                {
                    model.IdentifierSet = await identifiersApi.GetSiblingsBySourceIdentifier(
                        model.SourceSystem, model.Value, model.Type);
                    model.NotFound = model.IdentifierSet == null;
                }
                else
                {
                    model.CanonicalIdRef = await identifiersApi.GetBySourceIdentifier(
                        model.SourceSystem, model.Value, model.Type);
                    model.NotFound = model.CanonicalIdRef == null;
                }
            }
        }
        catch (IdentifiersApiException ex)
        {
            model.ErrorMessage = ex.Message;
        }

        return View(model);
    }

    /// <summary>
    /// Attempt to resolve any identifier string: a canonical id, a b number, a CALM ref, a GUID...
    /// This is the page to link to from elsewhere in the dashboard.
    /// </summary>
    [HttpGet("Resolve/{**id}")]
    public async Task<ActionResult> Resolve(string id)
    {
        var model = NewModel();
        model.ResolveInput = id?.Trim();
        if (model.ResolveInput.HasText())
        {
            model.Resolution = await identifiersApi.Resolve(model.ResolveInput);
        }
        return View("Index", model);
    }

    /// <summary>
    /// Target for the resolve form, so that the identifier ends up in the path rather than the query string.
    /// </summary>
    [HttpGet("ResolveForm")]
    public ActionResult ResolveForm(string id)
    {
        if (!id.HasText())
        {
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Resolve), new { id = id.Trim() });
    }

    private IdentifiersModel NewModel()
    {
        return new IdentifiersModel
        {
            BaseUrl = options.BaseUrl,
            HasApiKey = options.ApiKey.HasText()
        };
    }
}
