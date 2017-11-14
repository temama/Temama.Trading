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
    public class CexOrderBook : OrderBook
    {
        public static CexOrderBook FromJson(JObject json)
        {
            var res = new CexOrderBook();
            foreach (JArray orderJson in (json["asks"] as JArray))
            {
                var ord = new CexOrder()
                {
                    Price = Convert.ToDouble((orderJson[0] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Volume = Convert.ToDouble((orderJson[1] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Side = "sell"
                };                
                res.Asks.Add(ord);
            }
            res.Asks.Sort(CexOrder.SortByPrice);

            foreach (JArray orderJson in (json["bids"] as JArray))
            {
                var ord = new CexOrder()
                {
                    Price = Convert.ToDouble((orderJson[0] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Volume = Convert.ToDouble((orderJson[1] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Side = "buy"
                };
                res.Bids.Add(ord);
            }
            res.Bids.Sort(CexOrder.SortByPriceDesc);

            return res;
        }
    }
}
