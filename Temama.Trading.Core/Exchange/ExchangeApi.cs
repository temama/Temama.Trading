using System.Collections.Generic;
using System.Xml;
using System;

namespace Temama.Trading.Core.Exchange
{
    public abstract class ExchangeApi
    {
        protected Logger.Logger _log;
        protected bool _publicOnly = true;

        public virtual string Name()
        {
            return "GenericAPI";
        }
        
        private ExchangeApi()
        {

        }

        public ExchangeApi(XmlNode config, Logger.Logger logger)
        {
            _log = logger;
            Init(config);
        }
        
        public abstract double GetLastPrice(string baseCur, string fundCur);

        public abstract OrderBook GetOrderBook(string baseCur, string fundCur);
        
        public Funds GetFunds(string baseCur, string fundCur)
        {
            CheckNonPublicAllowed("GetFunds");

            return GetFundsImpl(baseCur, fundCur);
        }

        public Order PlaceOrder(string baseCur, string fundCur, string side, double volume, double price)
        {
            CheckNonPublicAllowed("PlaceOrder");

            return PlaceOrderImpl(baseCur, fundCur, side, volume, price);
        }

        public void CancellOrder(Order order)
        {
            CheckNonPublicAllowed("CancellOrder");

            CancellOrderImpl(order);
        }

        public List<Order> GetMyOrders(string baseCur, string fundCur)
        {
            CheckNonPublicAllowed("GetMyOrders");

            return GetMyOrdersImpl(baseCur, fundCur);
        }

        public List<Trade> GetMyTrades(string baseCur, string fundCur)
        {
            CheckNonPublicAllowed("GetMyTrades");

            return GetMyTradesImpl(baseCur, fundCur);
        }

        public abstract void Withdraw(string currency, string wallet);

        public virtual double CalculateBuyVolume(double price, double fund)
        {
            return Math.Round(Math.Floor(fund) / price, 6);
        }

        public virtual double GetRoundedSellVolume(double vol)
        {
            return Math.Round(vol, 6);
        }

        protected abstract void Init(XmlNode config);

        protected abstract Funds GetFundsImpl(string baseCur, string fundCur);

        protected abstract Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price);

        protected abstract void CancellOrderImpl(Order order);

        protected abstract List<Order> GetMyOrdersImpl(string baseCur, string fundCur);

        protected abstract List<Trade> GetMyTradesImpl(string baseCur, string fundCur);

        private void CheckNonPublicAllowed(string methodName = "The")
        {
            if (_publicOnly)
                throw new InvalidOperationException($"{Name()} works in PublicApiOnly mode. {methodName} method is not allowed");
        }
    }
}
