using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Exchanges.Kuna
{
    public class KunaApi : ExchangeApi, IExchangeAnalitics
    {
        private string _baseUri = "https://kuna.io/api/v2/";
        private string _publicKey;
        private string _secretKey;

        public override string Name()
        {
            return "KUNA.IO";
        }

        public KunaApi(XmlNode config, Logger logger) : base(config, logger)
        {
        }

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
            var pair = baseCur + fundCur;
            var uri = _baseUri + "tickers/" + pair;
            _log.Spam("Request: " + uri);
            var tickerResponse = WebApi.Query(uri);
            _log.Spam("Response: " + tickerResponse);
            var json = JObject.Parse(tickerResponse);
            var last = (JValue)json["ticker"]["last"];
            return Convert.ToDouble(last.Value, CultureInfo.InvariantCulture);
        }
        
        public override OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            var pair = baseCur + fundCur;
            var uri = _baseUri + "order_book?market=" + pair;
            _log.Spam("Request: " + uri);
            var response = WebApi.Query(uri);
            _log.Spam("GetOrderBoor: response: " + response);
            return KunaOrderBook.FromJson(JObject.Parse(response));
        }

        protected override Funds GetFundsImpl(string baseCur, string fundCur)
        {
            var response = UserQuery("members/me", "GET", new Dictionary<string, string>());
            _log.Spam("Response: " + response);
            var json = JObject.Parse(response);
            return KunaFunds.FromUserInfo(json, new List<string>() { baseCur, fundCur });
        }

        protected override Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price)
        {
            var pair = baseCur + fundCur;
            _log.Spam($"KUNA: Placing order: {side}:{pair}:{volume}:{price}");
            if (volume == 0 || price == 0)
                throw new Exception($"PlaceOrder: Volume or Price can't be zero. Vol={volume}; Price={price}");

            var response = UserQuery("orders", "POST", new Dictionary<string, string>(){
                { "side", side },
                { "volume", volume.ToString(CultureInfo.InvariantCulture) },
                { "market", pair },
                { "price", price.ToString(CultureInfo.InvariantCulture) }
            });
            _log.Spam("Response: " + response);
            
            var resp = KunaOrder.FromJson(JObject.Parse(response));
            return resp;
        }

        protected override void CancellOrderImpl(Order order)
        {
            var response = UserQuery("order/delete", "POST", new Dictionary<string, string>()
                { { "id", order.Id.ToString() } });
            _log.Spam("Response: " + response);
            
            var resp = KunaOrder.FromJson(JObject.Parse(response));
        }

        protected override List<Order> GetMyOrdersImpl(string baseCur, string fundCur)
        {
            var pair = baseCur + fundCur;
            var orders = new List<Order>();
            var response = UserQuery("orders", "GET", new Dictionary<string, string>() { { "market", pair } });
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                orders.Add(KunaOrder.FromJson(jsonOrder as JObject));
            }
            return orders;
        }

        protected override List<Trade> GetMyTradesImpl(string baseCur, string fundCur)
        {
            var pair = baseCur + fundCur;
            var trades = new List<Trade>();
            var response = UserQuery("trades/my", "GET", new Dictionary<string, string>() { { "market", pair } });
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                trades.Add(KunaTrade.FromJson(jsonOrder as JObject));
            }
            trades.Sort(Trade.SortByDate);
            return trades;
        }
        
        public List<Tick> GetRecentPrices(string baseCur, string fundCur, DateTime fromDate, int maxResultCount = 100)
        {
            var uri = _baseUri + "trades?market=" + baseCur.ToLower() + fundCur.ToLower();
            var response = WebApi.Query(uri);

            var ticks = new List<Tick>(maxResultCount);
            var json = JArray.Parse(response);
            foreach (var jTick in json)
            {
                var time = DateTime.Parse((jTick["created_at"] as JValue).Value.ToString());
                if (time >= fromDate)
                {
                    ticks.Add(new Tick()
                    {
                        Time = time,
                        Last = Convert.ToDouble(jTick["price"].ToString(), CultureInfo.InvariantCulture)
                    });
                }
            }
            return ticks;
        }

        /// <summary>
        /// Executes user queries (which require impersonalisation)
        /// </summary>
        /// <param name="path">api method path, like "trades/my"</param>
        /// <param name="method">GET or POST</param>
        /// <param name="args">arguments</param>
        /// <returns></returns>
        private string UserQuery(string path, string method, Dictionary<string, string> args)
        {
            args["access_key"] = _publicKey;
            args["tonce"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            args["signature"] = GenerateSignature(path, method, args);
            var dataStr = BuildPostData(args, true);
            _log.Spam($"{method}: {_baseUri}{path}?{dataStr}");

            if (method == "POST")
            {
                var request = WebRequest.Create(new Uri(_baseUri + path + "?" + dataStr)) as HttpWebRequest;
                if (request == null)
                    throw new Exception("Non HTTP WebRequest: " + _baseUri + path);
                
                request.Method = method;
                request.Timeout = 15000;
                request.ContentType = "application/x-www-form-urlencoded";
                return new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
            }
            else
            {
                return WebApi.Query(_baseUri + path + "?" + dataStr);
            }
        }

        private string GenerateSignature(string path, string method, Dictionary<string, string> args)
        {
            var uri = "/api/v2/" + path;
            var sortetDict = new SortedDictionary<string, string>(args);
            var sortedArgs = BuildPostData(sortetDict, true);
            var msg = method + "|" + uri + "|" + sortedArgs;  // "HTTP-verb|URI|params"
            var key = Encoding.ASCII.GetBytes(_secretKey);
            var msgBytes = Encoding.ASCII.GetBytes(msg);
            using (var hmac = new HMACSHA256(key))
            {
                byte[] hashmessage = hmac.ComputeHash(msgBytes);
                return BitConverter.ToString(hashmessage).Replace("-", string.Empty).ToLower();
            }
        }

        private static string BuildPostData(IDictionary<string, string> dict, bool escape = true)
        {
            return string.Join("&", dict.Select(kvp =>
                 string.Format("{0}={1}", kvp.Key, escape ? HttpUtility.UrlEncode(kvp.Value) : kvp.Value)));
        }
    }
}
