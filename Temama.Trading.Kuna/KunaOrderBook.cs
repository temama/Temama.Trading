using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Kuna
{
    public class KunaOrderBook
    {
        /// <summary>
        /// Sell orders
        /// </summary>
        public List<KunaOrder> Asks { get; private set; }

        /// <summary>
        /// Buy orders
        /// </summary>
        public List<KunaOrder> Bids { get; private set; }

        public KunaOrderBook()
        {
            Asks = new List<KunaOrder>();
            Bids = new List<KunaOrder>();
        }

        /// <summary>
        /// Find price for sell order to cover needed sell-amount according to order book
        /// </summary>
        /// <param name="amountBtc">Amount to sell</param>
        /// <returns>0.0 if not enough orders to cover amount</returns>
        public double FindPriceForSell(double amountBtc)
        {
            // Assumming Bids are sorted by price desc
            var currentAmount = 0.0;
            foreach (var ord in Bids)
            {
                currentAmount += ord.Volume;
                if (currentAmount > amountBtc)
                    return ord.Price;
            }

            return 0.0;
        }

        /// <summary>
        /// Find price for buy order to cover needed buy-amount according to order book
        /// </summary>
        /// <param name="amountUah">Amount to buy</param>
        /// <returns>double.MaxValue if not enough orders to cover amount</returns>
        public double FindPriceForBuy(double amountUah)
        {
            // Assuming Asks are sorted by price increasing
            var currentAmount = 0.0;
            foreach(var ord in Asks)
            {
                currentAmount += ord.Volume * ord.Price; // Getting volume in UAH
                if (currentAmount > amountUah)
                    return ord.Price;
            }

            return double.MaxValue;
        }

        public static KunaOrderBook FromJson(JObject json)
        {
            var res = new KunaOrderBook();
            foreach (JObject orderJson in (json["asks"] as JArray))
            {
                var ord = KunaOrder.FromJson(orderJson);
                ord.Volume = Convert.ToDouble((orderJson["remaining_volume"] as JValue).
                    Value.ToString(), CultureInfo.InvariantCulture);
                res.Asks.Add(ord);
            }
            res.Asks.Sort(KunaOrder.SortByPrice);

            foreach (JObject orderJson in (json["bids"] as JArray))
            {
                var ord = KunaOrder.FromJson(orderJson);
                ord.Volume = Convert.ToDouble((orderJson["remaining_volume"] as JValue).
                    Value.ToString(), CultureInfo.InvariantCulture);
                res.Bids.Add(ord);
            }
            res.Bids.Sort(KunaOrder.SortByPriceDesc);
            return res;
        }
    }
}
