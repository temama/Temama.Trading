using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Temama.Trading.Kuna
{
    public class KunaUserFunds
    {
        public double Btc { get; set; }
        public double Uah { get; set; }

        public override string ToString()
        {
            return string.Format("UserFunds:[BTC:{0}, UAH:{1}]", Btc, Uah);
        }

        public static KunaUserFunds FromUserInfo(JObject json)
        {
            var res = new KunaUserFunds();

            foreach(JObject acc in (json["accounts"] as JArray))
            {
                if (acc["currency"].ToString() == "btc")
                    res.Btc = Convert.ToDouble(acc["balance"].ToString(), CultureInfo.InvariantCulture);
                else if (acc["currency"].ToString() == "uah")
                    res.Uah = Convert.ToDouble(acc["balance"].ToString(), CultureInfo.InvariantCulture);
            }

            return res;
        }
    }
}
