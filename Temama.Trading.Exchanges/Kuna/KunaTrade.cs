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
    public class KunaTrade : Trade
    {
        public double Funds { get; set; }

        public static KunaTrade FromJson(JObject json)
        {
            return new KunaTrade()
            {
                Id = ((json["id"] as JValue).Value).ToString(),
                Pair = ((json["market"] as JValue).Value).ToString(),
                Side = (((json["side"] as JValue).Value == null) ? "" : 
                    ((json["side"] as JValue).Value.ToString() == "ask" ? "sell" : "buy")),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["volume"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Funds = Convert.ToDouble((json["funds"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                CreatedAt = DateTime.Parse((json["created_at"] as JValue).Value.ToString()).ToUniversalTime()
            };
        }
    }
}
