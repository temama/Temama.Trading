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
    public class KunaOrder : Order
    {
        public static KunaOrder FromJson(JObject json)
        {
            var order = new KunaOrder()
            {
                Id = (json["id"] as JValue).Value.ToString(),
                Pair = (json["market"] as JValue).Value.ToString(),
                Side = (json["side"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["volume"] as JValue).Value.ToString(), CultureInfo.InvariantCulture)
            };
            return order;
        }
    }
}
