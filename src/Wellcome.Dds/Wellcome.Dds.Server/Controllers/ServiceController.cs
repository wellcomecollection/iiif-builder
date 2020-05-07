using Microsoft.AspNetCore.Mvc;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{
    public class ServiceController : ControllerBase
    {
        [Route("service/helloworld/{id}")]
        public IActionResult HelloWorld(string id)
        {
            var normalised = WellcomeLibraryIdentifiers.GetNormalisedBNumber(id, false);
            if (normalised == id)
            {
                return Ok("That's a good b number!");
            }
            else if (normalised == null)
            {
                return Ok("I don't know what that is.");
            }
            else
            {
                return Ok($"I know what you mean, but {normalised} is the canonical form of {id}.");
            }
        }
    }
}