﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Exchanges.Cex
{
    public class CexTrade : Trade
    {
        public static CexTrade FromJson(JObject json)
        {
            var res = new CexTrade()
            {
                Id = (json["id"] as JValue).Value.ToString(),
                Side = (json["type"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["amount"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                CreatedAt = DateTime.Parse((json["time"] as JValue).Value.ToString())
            };

            if (json["symbol1"] != null && json["symbol2"] != null)
            {
                res.Pair = ((json["symbol1"] as JValue).Value.ToString() + (json["symbol2"] as JValue).Value.ToString()).ToLower();
                res.Funds = Convert.ToDouble((json["tta:" + (json["symbol2"] as JValue).Value.ToString()] as JValue).Value.ToString(), CultureInfo.InvariantCulture);
            }

            return res;
        }
    }
}
