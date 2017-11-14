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
    public class CexOrder : Order
    {
        public static CexOrder FromJson(JObject json)
        {
            var res = new CexOrder()
            {
                Id = (json["id"] as JValue).Value.ToString(),
                Side = (json["type"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["amount"] as JValue).Value.ToString(), CultureInfo.InvariantCulture)
            };

            if (json["symbol1"] != null && json["symbol2"] != null)
                res.Pair = ((json["symbol1"] as JValue).Value.ToString() + (json["symbol2"] as JValue).Value.ToString()).ToLower();

            return res;
        }
    }
}
