using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Utils;
using Utils.Web;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig;

namespace Wellcome.Dds.Server.Controllers
{   
    [Route("[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private IDds dds;
        
        public ServiceController(IDds dds)
        {
            this.dds = dds;
        }       
        
        [HttpGet("suggest-b-number")]
        public IActionResult SuggestBNumber(string q)
        {
            if (!q.HasText()) return Ok(System.Array.Empty<object>());
            if (q == "imfeelinglucky")
            {
                Response.AppendStandardNoCacheHeaders();
            }
            var suggestions = dds.AutoComplete(q);
            return Ok(suggestions.Select(fm => new AutoCompleteSuggestion
            {
                Id = fm.PackageIdentifier,
                Label = fm.PackageLabel
            }));
        }

        [HttpGet("playerconfig")]
        public IActionResult PlayerConfig(string v = null)
        {
            PlayerConfig cfg = "moh".Equals(v)
                ? PlayerConfigProvider.MoHConfig
                : PlayerConfigProvider.BaseConfig;
            return new JsonResult(cfg);
        }
        
    }
    
    public class AutoCompleteSuggestion
    {
        public string Id { get; set; }
        public string Label { get; set; }
    }
}