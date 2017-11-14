using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Exchanges.Kuna
{
    public class KunaFunds : Funds
    {
        public static KunaFunds FromUserInfo(JObject json, List<string> currencies)
        {
            var res = new KunaFunds();

            foreach (JObject acc in (json["accounts"] as JArray))
            {
                if (currencies.Contains(acc["currency"].ToString().ToLower()))
                {
                    res.Values[acc["currency"].ToString()] = Convert.ToDouble(acc["balance"].ToString(), CultureInfo.InvariantCulture);
                }
            }

            return res;
        }
    }
}
