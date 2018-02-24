using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Exchanges.Cex
{
    public class CexFunds : Funds
    {
        public static CexFunds FromUserInfo(JObject json, List<string> currencies)
        {
            var res = new CexFunds();

            foreach (var cur in currencies)
            {
                res.Values[cur.ToUpper()] = Convert.ToDouble(json[cur.ToUpper()]["available"].ToString(), CultureInfo.InvariantCulture);
            }

            return res;
        }
    }
}
