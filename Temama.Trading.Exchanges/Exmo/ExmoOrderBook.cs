using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoOrderBook : OrderBook
    {
        public static ExmoOrderBook FromJson(JObject json)
        {
            var res = new ExmoOrderBook();
            foreach (JArray orderJson in (json["ask"] as JArray))
            {
                res.Asks.Add(new ExmoOrder()
                {
                    Price = Convert.ToDouble((orderJson[0] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Volume = Convert.ToDouble((orderJson[1] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Side = "sell"
                });
            }
            res.Asks.Sort(Order.SortByPrice);

            foreach (JArray orderJson in (json["bid"] as JArray))
            {
                res.Bids.Add(new ExmoOrder()
                {
                    Price = Convert.ToDouble((orderJson[0] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Volume = Convert.ToDouble((orderJson[1] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                    Side = "buy"
                });
            }
            res.Bids.Sort(Order.SortByPriceDesc);
            return res;
        }
    }
}
