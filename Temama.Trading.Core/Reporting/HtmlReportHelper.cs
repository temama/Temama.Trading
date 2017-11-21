using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Algo;

namespace Temama.Trading.Core.Reporting
{
    public static class HtmlReportHelper
    {
        public static string ReportRunningBots(List<Algorithm> bots)
        {
            var res = new StringBuilder();
            res.Append("<h3>Running bots</h3>");
            foreach (var bot in bots)
            {
                res.Append(bot.WhoAmI + "\r\n");
            }
            res.Append("<hr/>");
            return res.ToString();
        }
    }
}
