using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace Temama.Trading.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppLogController : ControllerBase
    {
        [HttpGet("[action]")]
        public IEnumerable<string> GetAppLogLines()
        {
            var all = GetAppLog();
            return new List<string>();
        }

        [HttpGet("[action]")]
        public string GetAppLog()
        {
            return "Here will be log\r\nof the app";
        }
    }
}