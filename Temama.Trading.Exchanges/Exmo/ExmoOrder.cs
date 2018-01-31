using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoOrder : Order
    {
        public static ExmoOrder FromJson(JObject json)
        {
            var res = new ExmoOrder()
            {
                Id = (json["order_id"] as JValue).Value.ToString(),
                CreatedAt = UnixTime.FromUnixTimeMillis(Convert.ToInt64(json["created"].ToString())),
                Side = (json["type"] as JValue).Value.ToString(),
                Pair = (json["pair"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["quantity"] as JValue).Value.ToString(), CultureInfo.InvariantCulture)
            };
            
            return res;
        }
    }
}
