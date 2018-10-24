using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace Temama.Trading.Web.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class AppLogController : ControllerBase
    {
        [HttpGet("[action]")]
        public IActionResult GetAppLogLines()
        {
            var all = GetAppLog();
            return Ok(new List<string>());
        }

        [HttpGet("[action]")]
        public IActionResult GetAppLog()
        {
            return Ok("Here will be log\r\nof the app");
        }
    }
}