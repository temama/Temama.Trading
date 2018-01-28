using System;
using System.Collections.Generic;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoApi : ExchangeApi, IExchangeAnalitics, IExchangeEmulator
    {
        private string _baseUri = "http://api.exmo.com/v1/";
        private string _publicKey;
        private string _secretKey;

        public override string Name()
        {
            return "Exmo.com";
        }

        public ExmoApi(XmlNode config, Logger logger) : base(config, logger)
        { }

        protected override void Init(XmlNode config)
        {
            _publicKey = config.GetConfigValue("PublicKey", true);
            _secretKey = config.GetConfigValue("SecretKey", true);

            _publicOnly = (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey));
            if (_publicOnly)
                _log.Info($"{Name()} inited in PublicApiOnly mode");
        }

        public override double GetLastPrice(string baseCur, string fundCur)
        {
            throw new System.NotImplementedException();
        }

        public override OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            throw new System.NotImplementedException();
        }

        public List<Tick> GetRecentPrices(string baseCur, string fundCur, DateTime fromDate, int maxResultCount = 1000)
        {
            throw new NotImplementedException();
        }

        public void SetIterationTime(DateTime time)
        {
            throw new NotImplementedException();
        }

        protected override void CancellOrderImpl(Order order)
        {
            throw new System.NotImplementedException();
        }

        protected override Funds GetFundsImpl(string baseCur, string fundCur)
        {
            throw new System.NotImplementedException();
        }

        protected override List<Order> GetMyOrdersImpl(string baseCur, string fundCur)
        {
            throw new System.NotImplementedException();
        }

        protected override List<Trade> GetMyTradesImpl(string baseCur, string fundCur)
        {
            throw new System.NotImplementedException();
        }


        protected override Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price)
        {
            throw new System.NotImplementedException();
        }
    }
}
