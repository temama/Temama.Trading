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
    public class KunaOrderBook : OrderBook
    {
        public static KunaOrderBook FromJson(JObject json)
        {
            var res = new KunaOrderBook();
            foreach (JObject orderJson in (json["asks"] as JArray))
            {
                var ord = KunaOrder.FromJson(orderJson);
                ord.Volume = Convert.ToDouble((orderJson["remaining_volume"] as JValue).
                    Value.ToString(), CultureInfo.InvariantCulture);
                res.Asks.Add(ord);
            }
            res.Asks.Sort(KunaOrder.SortByPrice);

            foreach (JObject orderJson in (json["bids"] as JArray))
            {
                var ord = KunaOrder.FromJson(orderJson);
                ord.Volume = Convert.ToDouble((orderJson["remaining_volume"] as JValue).
                    Value.ToString(), CultureInfo.InvariantCulture);
                res.Bids.Add(ord);
            }
            res.Bids.Sort(KunaOrder.SortByPriceDesc);

            return res;
        }
    }
}
