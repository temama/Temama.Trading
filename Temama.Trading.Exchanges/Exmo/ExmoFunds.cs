using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoFunds : Funds
    {
        public static ExmoFunds FromUserInfo(JObject json, List<string> currencies)
        {
            var res = new ExmoFunds();

            foreach (var cur in currencies)
            {
                res.Values[cur] = Convert.ToDouble(json["balances"][cur.ToUpper()].ToString(), CultureInfo.InvariantCulture);
            }

            return res;
        }
    }
}
