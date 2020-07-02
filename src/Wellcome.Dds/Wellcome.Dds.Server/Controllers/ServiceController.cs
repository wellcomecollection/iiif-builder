using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ServiceController(IConfiguration config)
        {
            _config = config;
        }
        
        /// <summary>
        /// Test HelloWorld action method. Takes a bnumber and tries to normalise it.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /service/helloworld/B1675665
        /// 
        /// </remarks>
        /// <param name="id">The Id to normalise.</param>
        /// <returns>Normalised bnumber.</returns>
        [ProducesResponseType(200)]
        [Route("service/helloworld/{id}")]
        [HttpGet]
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

        [Route("service/conn/{name}")]
        [HttpGet]
        public IActionResult ConnCheck(string name)
        {
            try
            {
                var conn = _config.GetConnectionString(name);
                var connection = new NpgsqlConnection(conn);
                connection.Open();
                return Ok("Connected");
            }
            catch (Exception e)
            {
                return Problem(detail: e.Message, statusCode: 500, title: "Fail");
            }
        }
    }
}