using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public class OrderBook
    {
        /// <summary>
        /// Sell orders
        /// </summary>
        public List<Order> Asks { get; private set; }

        /// <summary>
        /// Buy orders
        /// </summary>
        public List<Order> Bids { get; private set; }

        public OrderBook()
        {
            Asks = new List<Order>();
            Bids = new List<Order>();
        }

        /// <summary>
        /// Find price for sell order to cover needed sell-amount according to order book
        /// </summary>
        /// <param name="amountBase">Amount to sell</param>
        /// <returns>0.0 if not enough orders to cover amount</returns>
        public double FindPriceForSell(double amountBase)
        {
            // Assumming Bids are sorted by price desc
            var currentAmount = 0.0;
            foreach (var ord in Bids)
            {
                currentAmount += ord.Volume;
                if (currentAmount > amountBase)
                    return ord.Price;
            }

            return 0.0;
        }

        /// <summary>
        /// Find price for buy order to cover needed buy-amount according to order book
        /// </summary>
        /// <param name="amountFunds">Amount to buy</param>
        /// <returns>double.MaxValue if not enough orders to cover amount</returns>
        public double FindPriceForBuy(double amountFunds)
        {
            // Assuming Asks are sorted by price increasing
            var currentAmount = 0.0;
            foreach (var ord in Asks)
            {
                currentAmount += ord.Volume * ord.Price; // Getting volume in UAH
                if (currentAmount > amountFunds)
                    return ord.Price;
            }

            return double.MaxValue;
        }
    }
}
