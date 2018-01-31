using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
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

namespace Temama.Trading.Exchanges.Exmo
{
    public class ExmoApi : ExchangeApi, IExchangeAnalitics
    {
        private int _tradesFetchCount = 20;
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
            var uri = _baseUri + "ticker/";
            _log.Spam("Request: " + uri);
            var tickerResponse = WebApi.Query(uri);
            _log.Spam("Response: " + tickerResponse);
            var json = JObject.Parse(tickerResponse);
            var last = (JValue)json[$"{baseCur.ToUpper()}_{fundCur.ToUpper()}"]["last_trade"];
            return Convert.ToDouble(last.Value, CultureInfo.InvariantCulture);
        }

        public override OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            var uri = _baseUri + $"order_book/?pair={baseCur.ToUpper()}_{fundCur.ToUpper()}/";
            _log.Spam("Request: " + uri);
            var response = WebApi.Query(uri);
            _log.Spam("GetOrderBoor: response: " + response);
            return ExmoOrderBook.FromJson(JObject.Parse(response));
        }
        
        protected override void CancellOrderImpl(Order order)
        {
            var response = ApiQuery("order_cancel", new Dictionary<string, string>()
                { { "order_id", order.Id.ToString() } });
            _log.Spam("Response: " + response);

            var json = JObject.Parse(response);
            if (((JValue)json["result"]).Value.ToString().ToLower() != "true")
                throw new Exception($"{Name()}: Failed to cancel order {order}. Response: {response}");
        }

        protected override Funds GetFundsImpl(string baseCur, string fundCur)
        {
            var response = ApiQuery("user_info", new Dictionary<string, string>());
            _log.Spam("Response: " + response);

            var json = JObject.Parse(response);
            return ExmoFunds.FromUserInfo(json, new List<string> { baseCur, fundCur });
        }

        protected override Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price)
        {
            var pair = baseCur.ToUpper() + "_" + fundCur.ToUpper();
            _log.Spam($"{Name()}: Placing order: {side}:{pair}:{volume}:{price}");
            if (volume == 0 || price == 0)
                throw new Exception($"PlaceOrder: Volume or Price can't be zero. Vol={volume}; Price={price}");

            var response = ApiQuery("order_create",
                new Dictionary<string, string>(){
                { "pair", pair},
                { "quantity", volume.ToString(CultureInfo.InvariantCulture) },
                { "price", price.ToString(CultureInfo.InvariantCulture) },
                { "type", side}
            });
            _log.Spam("Response: " + response);

            var jResp = JObject.Parse(response);
            if (((JValue)jResp["result"]).Value.ToString().ToLower() != "true")
                throw new Exception($"{Name()}: Failed to place order. Response: {response}");
            var resp = new ExmoOrder() {
                Id = ((JValue)jResp["order_id"]).Value.ToString(),
                CreatedAt = DateTime.UtcNow,
                Pair = pair,
                Price = price,
                Volume = volume,
                Side = side
            };
            return resp;
        }

        protected override List<Order> GetMyOrdersImpl(string baseCur, string fundCur)
        {
            var orders = new List<Order>();
            var response = ApiQuery("user_open_orders", new Dictionary<string, string>());
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json[$"{baseCur.ToUpper()}_{fundCur.ToUpper()}"])
            {
                orders.Add(ExmoOrder.FromJson(jsonOrder as JObject));
            }
            orders.Sort(Order.SortByPrice);
            return orders;
        }

        protected override List<Trade> GetMyTradesImpl(string baseCur, string fundCur)
        {
            var trades = new List<Trade>();
            var pair = baseCur.ToUpper() + "_" + fundCur.ToUpper();
            var response = ApiQuery("user_trades",
                new Dictionary<string, string>() {
                    { "pair", pair },
                    { "offset ", "0" },
                    { "limit", _tradesFetchCount.ToString() }
                });
            _log.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json[pair])
            {
                trades.Add(ExmoTrade.FromJson(jsonOrder as JObject));
            }
            trades.Sort(Trade.SortByDate);
            return trades;
        }

        public List<Trade> GetRecentTrades(string baseCur, string fundCur, DateTime fromDate)
        {
            var pair = baseCur.ToUpper() + "_" + fundCur.ToUpper();
            var uri = _baseUri + "trades/?pair=" + pair;
            var response = WebApi.Query(uri);

            var trades = new List<Trade>();
            var json = JArray.Parse(response);
            foreach (JObject jTrade in json[pair])
            {
                var time = UnixTime.FromUnixTime(Convert.ToInt64(jTrade["date"].ToString()));
                if (time >= fromDate)
                {
                    trades.Add(ExmoTrade.FromJson(jTrade));
                }
            }
            return trades;
        }

        private string ApiQuery(string apiName, IDictionary<string, string> req)
        {
            using (var wb = new WebClient())
            {
                req.Add("nonce", Convert.ToString(UnixTime.GetUnixTime()));
                var message = ToQueryString(req);

                var sign = Sign(_secretKey, message);

                wb.Headers.Add("Sign", sign);
                wb.Headers.Add("Key", _publicKey);

                var data = ToNameValueCollection(req);

                var response = wb.UploadValues(string.Format(_baseUri, apiName), "POST", data);
                return Encoding.UTF8.GetString(response);
            }
        }

        private string ToQueryString(IDictionary<string, string> dic)
        {
            var array = (from key in dic.Keys
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(dic[key])))
                .ToArray();
            return string.Join("&", array);
        }

        private static string Sign(string key, string message)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return ByteToString(b);
            }
        }

        private static NameValueCollection ToNameValueCollection(IDictionary<string, string> dict)
        {
            var nameValueCollection = new NameValueCollection();

            foreach (var kvp in dict)
            {
                string value = string.Empty;
                if (kvp.Value != null)
                    value = kvp.Value.ToString();

                nameValueCollection.Add(kvp.Key.ToString(), value);
            }

            return nameValueCollection;
        }

        private static string ByteToString(byte[] buff)
        {
            string sbinary = "";

            for (int i = 0; i < buff.Length; i++)
            {
                sbinary += buff[i].ToString("X2"); // hex format
            }
            return (sbinary).ToLowerInvariant();
        }
    }
}
