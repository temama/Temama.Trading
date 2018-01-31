using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoTrade : Trade
    {
        public static ExmoTrade FromJson(JObject json)
        {
            var res = new ExmoTrade()
            {
                Id = (json["trade_id"] as JValue).Value.ToString(),
                Side = (json["type"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["quantity"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                CreatedAt = UnixTime.FromUnixTimeMillis(Convert.ToInt64(json["date"].ToString())),
                Pair = (json["pair"] as JValue).Value.ToString()
            };

            return res;
        }
    }
}
