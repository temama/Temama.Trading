using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Exchanges.Cex
{
    public class CexApi : ExchangeApi, IExchangeAnalitics
    {
        private object _locker = new object();
        private string _baseUri = "https://cex.io/api/";
        private string _publicKey;
        private string _secretKey;
        private string _userId;
        private int _OrderBookDepth = 20;
        private int _tradesFetchCount = 20;
        private int _tradeHistoryResponseCount = 1000;

        private Dictionary<string, List<Trade>> _historical = new Dictionary<string, List<Trade>>();
        private Dictionary<string, TimeSpan> _historicalPersistIntervals = new Dictionary<string, TimeSpan>();

        public override string Name()
        {
            return "CEX.IO";
        }

        public CexApi(XmlNode config, Logger logger) : base(config, logger) { }

        protected override void Init(XmlNode config)
        {
            _publicKey = config.GetConfigValue("PublicKey", true);
            _secretKey = config.GetConfigValue("SecretKey", true);
            _userId = config.GetConfigValue("UserID", true);

            _publicOnly = (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey) || string.IsNullOrEmpty(_userId));
            if (_publicOnly)
                _log.Info($"{Name()} inited in PublicApiOnly mode");
        }

        public override double GetLastPrice(string baseCur, string fundCur)
        {
            var uri = _baseUri + "last_price/" + baseCur.ToUpper() + "/" + fundCur.ToUpper();
            _log.Spam("Request: " + uri);
            var tickerResponse = WebApi.Query(uri);
            _log.Spam("Response: " + tickerResponse);
            var json = JObject.Parse(tickerResponse);
            var last = (JValue)json["lprice"];
            return Convert.ToDouble(last.Value, CultureInfo.InvariantCulture);
        }

        public override OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            var uri = _baseUri + $"order_book/{baseCur.ToUpper()}/{fundCur.ToUpper()}/?depth={_OrderBookDepth}";
            _log.Spam("Request: " + uri);
            var response = WebApi.Query(uri);
            _log.Spam("GetOrderBoor: response: " + response);
            return CexOrderBook.FromJson(JObject.Parse(response));
        }

        protected override Funds GetFundsImpl(string baseCur, string fundCur)
        {
            var response = UserQuery("balance/", new Dictionary<string, string>());
            _log.Spam("Response: " + response);
            var json = JObject.Parse(response);
            return CexFunds.FromUserInfo(json, new List<string>() { baseCur, fundCur });
        }
        
        protected override Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price)
        {
            var pair = baseCur + fundCur;
            _log.Spam($"{Name()}: Placing order: {side}:{pair}:{volume}:{price}");
            if (volume == 0 || price == 0)
                throw new Exception($"PlaceOrder: Volume or Price can't be zero. Vol={volume}; Price={price}");

            var response = UserQuery($"place_order/{baseCur.ToUpper()}/{fundCur.ToUpper()}", 
                new Dictionary<string, string>(){
                { "type", side },
                { "amount", volume.ToString(CultureInfo.InvariantCulture) },
                { "price", price.ToString(CultureInfo.InvariantCulture) }
            });
            _log.Spam("Response: " + response);
            
            var resp = CexOrder.FromJson(JObject.Parse(response));
            resp.Pair = baseCur.ToUpper() + fundCur.ToUpper();
            return resp;
        }

        protected override void CancellOrderImpl(Order order)
        {
            var response = UserQuery("cancel_order/", new Dictionary<string, string>()
                { { "id", order.Id.ToString() } });
            _log.Spam("Response: " + response);
            
            if (response.ToLower() != "true")
                throw new Exception($"{Name()}: Failed to cancel order {order}. Response: {response}");
        }
                
        protected override List<Order> GetMyOrdersImpl(string baseCur, string fundCur)
        {
            var orders = new List<Order>();
            var response = UserQuery($"open_orders/{baseCur.ToUpper()}/{fundCur.ToUpper()}", new Dictionary<string, string>());
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                orders.Add(CexOrder.FromJson(jsonOrder as JObject));
            }
            orders.Sort(Order.SortByPrice);
            return orders;
        }

        protected override List<Trade> GetMyTradesImpl(string baseCur, string fundCur)
        {
            var trades = new List<Trade>();
            var response = UserQuery($"archived_orders/{baseCur.ToUpper()}/{fundCur.ToUpper()}",
                new Dictionary<string, string>() { { "limit", _tradesFetchCount.ToString() } });
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                trades.Add(CexTrade.FromJson(jsonOrder as JObject));
            }
            trades.Sort(Trade.SortByDate);
            return trades;
        }


        public List<Trade> GetRecentTrades(string baseCur, string fundCur, DateTime fromDate)
        {
            var uri = _baseUri + "trade_history/" + baseCur.ToUpper() + "/" + fundCur.ToUpper();
            var response = WebApi.Query(uri);
            var json = JArray.Parse(response);
            var trades = ParseTradesFromJson(json);
            trades.Sort(Trade.SortByDate);
            UpdateHistorical(baseCur, fundCur, trades);

            if (_historical[$"{baseCur}{fundCur}"].First().CreatedAt < fromDate)
                return _historical[$"{baseCur}{fundCur}"].Where(t => t.CreatedAt >= fromDate).ToList();

            while (trades.First().CreatedAt > fromDate)
            {
                uri = _baseUri + "trade_history/" + baseCur.ToUpper() + "/" + fundCur.ToUpper() +
                    "/?since=" + (Convert.ToInt64(trades.First().Id) - _tradeHistoryResponseCount).ToString();
                response = WebApi.Query(uri);
                json = JArray.Parse(response);
                trades.AddRange(ParseTradesFromJson(json));
                trades.Sort(Trade.SortByDate);
            }
            
            UpdateHistorical(baseCur, fundCur, trades);
            return trades.Where(t => t.CreatedAt >= fromDate).ToList();
        }

        public void SetHistoricalTradesPersistInterval(string baseCur, string fundCur, TimeSpan duration)
        {
            // Actually will save a bit more than "duration"
            _historicalPersistIntervals[$"{baseCur}{fundCur}"] = duration + TimeSpan.FromSeconds(duration.TotalSeconds / 2);
        }

        public bool HasHistoricalDataStartingFrom(string baseCur, string fundCur, DateTime dateTime, bool fetchLatest = false)
        {
            if (fetchLatest)
                GetRecentTrades(baseCur, fundCur, dateTime);

            return true;
        }

        private void UpdateHistorical(string baseCur, string fundCur, List<Trade> trades)
        {
            var lifetime = _historicalPersistIntervals[$"{baseCur}{fundCur}"];
            var historical = _historical[$"{baseCur}{fundCur}"];
            var toRemove = new List<Trade>();

            lock (_locker)
            {
                foreach (var t in historical)
                {
                    if (t.CreatedAt < DateTime.UtcNow - lifetime)
                        toRemove.Add(t);
                }

                foreach (var t in toRemove)
                {
                    historical.Remove(t);
                }

                foreach (var t in trades)
                {
                    if (!historical.Any(tt => tt.Id == t.Id))
                        historical.Add(t);
                }

                historical.Sort(Trade.SortByDate);
            }
        }

        private List<Trade> ParseTradesFromJson(JArray json)
        {
            var trades = new List<Trade>();
            foreach (JObject jTrade in json)
            {
                trades.Add(CexTrade.FromJson(jTrade));
            }
            return trades;
        }

        private string UserQuery(string path, Dictionary<string, string> args)
        {
            args["key"] = _publicKey;
            args["nonce"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            args["signature"] = GenerateSignature(args["nonce"]);

            return PostQuery(path, args);
        }

        private string PostQuery(string path, Dictionary<string, string> args)
        {
            var uri = _baseUri + path;
            using (var wb = new WebClient())
            {
                var data = new NameValueCollection();
                foreach (var kvp in args)
                {
                    data[kvp.Key] = kvp.Value;
                }
                return Encoding.ASCII.GetString(wb.UploadValues(uri, "POST", data));
            }
        }

        private string GenerateSignature(string nonce)
        {
            var bytes = Encoding.UTF8.GetBytes($"{nonce}{_userId}{_publicKey}");
            var key = Encoding.ASCII.GetBytes(_secretKey);
            using (var hmac = new HMACSHA256(key))
            {
                byte[] hashmessage = hmac.ComputeHash(bytes);
                return BitConverter.ToString(hashmessage).Replace("-", string.Empty).ToUpper();
            }
        }

        public override void Withdraw(string currency, string wallet)
        {
            throw new NotImplementedException();
        }
    }
}
