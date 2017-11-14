using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Temama.Trading.Core.Exchange
{
    public interface IExchangeApi
    {
        string Name();

        void Init(XmlDocument config);

        double GetLastPrice(string baseCur, string fundCur);

        OrderBook GetOrderBook(string baseCur, string fundCur);
        
        Funds GetFunds(string baseCur, string fundCur);

        Order PlaceOrder(string baseCur, string fundCur, string side, double volume, double price);

        void CancellOrder(Order order);

        /// <summary>
        /// Returns user's orders sorted by price
        /// </summary>
        /// <returns></returns>
        List<Order> GetMyOrders(string baseCur, string fundCur);

        /// <summary>
        /// Returns user's recent trades sorted by date (desc)
        /// </summary>
        /// <returns></returns>
        List<Trade> GetMyTrades(string baseCur, string fundCur);
    }
}
