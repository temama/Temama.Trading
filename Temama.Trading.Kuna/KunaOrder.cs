using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Kuna
{
    public class KunaOrder
    {
        public long Id { get; set; }
        public string Side { get; set; }
        public double Price { get; set; }
        public double Volume { get; set; }

        public override string ToString()
        {
            return string.Format("#{0}:{1}:{2}:{3}", Id, Side, Price, Volume);
        }

        public static int SortByPrice(KunaOrder first, KunaOrder second)
        {
            if (first.Price < second.Price)
                return -1;
            else if (second.Price < first.Price)
                return 1;
            else return 0;
        }

        public static int SortByPriceDesc(KunaOrder first, KunaOrder second)
        {
            if (first.Price < second.Price)
                return 1;
            else if (second.Price < first.Price)
                return -1;
            else return 0;
        }

        public static KunaOrder FromJson(JObject json)
        {
            var order = new KunaOrder()
            {
                Id = Convert.ToInt64((json["id"] as JValue).Value),
                Side = (json["side"] as JValue).Value.ToString(),
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["volume"] as JValue).Value.ToString(), CultureInfo.InvariantCulture)
            };
            return order;
        }
    }
}
