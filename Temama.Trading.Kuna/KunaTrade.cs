using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Kuna
{
    public class KunaTrade
    {
        public long Id { get; private set; }
        public double Price { get; private set; }
        public double Volume { get; private set; }
        public double Funds { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string Side { get; private set; }

        /// <summary>
        /// Sorts by date desc
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static int SortByDate(KunaTrade first, KunaTrade second)
        {
            if (first.CreatedAt < second.CreatedAt)
                return 1;
            else if (second.CreatedAt < first.CreatedAt)
                return -1;
            else return 0;
        }

        public override string ToString()
        {
            return string.Format("#{0}:{1}:{2}:{3}", Id, Side, Volume, Price);
        }

        public static KunaTrade FromJson(JObject json)
        {
            return new KunaTrade()
            {
                Id = Convert.ToInt64((json["id"] as JValue).Value),
                Side = (json["side"] as JValue).Value.ToString() == "ask" ? "sell" : "buy",
                Price = Convert.ToDouble((json["price"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Volume = Convert.ToDouble((json["volume"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                Funds = Convert.ToDouble((json["funds"] as JValue).Value.ToString(), CultureInfo.InvariantCulture),
                CreatedAt = DateTime.Parse((json["created_at"] as JValue).Value.ToString())
            };
        }
    }
}
