using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Exchanges.Cex
{
    public class CexApi : IExchangeApi
    {
        private string _baseUri = "https://cex.io/api/";
        private string _publicKey;
        private string _secretKey;
        private string _userId;
        private int _OrderBookDepth = 20;
        private int _tradesFetchCount = 20;

        public string Name()
        {
            return "CEX.IO";
        }

        public void Init(XmlDocument config)
        {
            var node = config.SelectSingleNode("//TemamaTradingConfig/PublicKey");
            _publicKey = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/SecretKey");
            _secretKey = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/UserID");
            _userId = node.InnerText;
        }

        public double GetLastPrice(string baseCur, string fundCur)
        {
            var uri = _baseUri + "last_price/" + baseCur.ToUpper() + "/" + fundCur.ToUpper();
            Logger.Spam("Request: " + uri);
            var tickerResponse = WebApi.Query(uri);
            Logger.Spam("Response: " + tickerResponse);
            var json = JObject.Parse(tickerResponse);
            var last = (JValue)json["lprice"];
            return Convert.ToDouble(last.Value, CultureInfo.InvariantCulture);
        }

        public OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            var uri = _baseUri + string.Format("order_book/{0}/{1}/?depth={2}", baseCur.ToUpper(), fundCur.ToUpper(), _OrderBookDepth);
            Logger.Spam("Request: " + uri);
            var response = WebApi.Query(uri);
            Logger.Spam("GetOrderBoor: response: " + response);
            return CexOrderBook.FromJson(JObject.Parse(response));
        }

        public Funds GetFunds(string baseCur, string fundCur)
        {
            var response = UserQuery("balance/", new Dictionary<string, string>());
            Logger.Spam("Response: " + response);
            var json = JObject.Parse(response);
            return CexFunds.FromUserInfo(json, new List<string>() { baseCur, fundCur });
        }
        
        public Order PlaceOrder(string baseCur, string fundCur, string side, double volume, double price)
        {
            var pair = baseCur + fundCur;
            Logger.Spam(string.Format("CEX.IO: Placing order: {0}:{1}:{2}:{3}", side, pair, volume, price));
            if (volume == 0 || price == 0)
                throw new Exception(string.Format("PlaceOrder: Volume or Price can't be zero. Vol={0}; Price={1}", volume, price));

            var response = UserQuery(string.Format("place_order/{0}/{1}", baseCur.ToUpper(), fundCur.ToUpper()), 
                new Dictionary<string, string>(){
                { "type", side },
                { "amount", volume.ToString(CultureInfo.InvariantCulture) },
                { "price", price.ToString(CultureInfo.InvariantCulture) }
            });
            Logger.Spam("Response: " + response);

            // Response of placing order is order JSON
            // Checking if query was successfull by trying to parse response as order
            var resp = CexOrder.FromJson(JObject.Parse(response));
            resp.Pair = baseCur.ToUpper() + fundCur.ToUpper();
            Logger.Warning(string.Format("CEX.IO: Order {0} placed", resp));
            return resp;
        }

        public void CancellOrder(Order order)
        {
            var response = UserQuery("cancel_order", new Dictionary<string, string>()
                { { "id", order.Id.ToString() } });
            Logger.Spam("Response: " + response);

            // Response of cancellation is order JSON
            // Checking if query was successfull by trying to parse response as order
            if (response.ToLower() != "true")
                throw new Exception(string.Format("CEX.IO: Failed to cancel order {0}. Response: {1}", order, response));
            Logger.Warning(string.Format("CEX.IO: Order {0} was cancelled", order));
        }
                
        public List<Order> GetMyOrders(string baseCur, string fundCur)
        {
            var orders = new List<Order>();
            var response = UserQuery(string.Format("open_orders/{0}/{1}", baseCur.ToUpper(), fundCur.ToUpper()), new Dictionary<string, string>());
            Logger.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                orders.Add(CexOrder.FromJson(jsonOrder as JObject));
            }
            orders.Sort(CexOrder.SortByPrice);
            return orders;
        }

        public List<Trade> GetMyTrades(string baseCur, string fundCur)
        {
            var trades = new List<Trade>();
            var response = UserQuery(string.Format("archived_orders/{0}/{1}", baseCur.ToUpper(), fundCur.ToUpper()), 
                new Dictionary<string, string>() { { "limit", _tradesFetchCount.ToString() } });
            Logger.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                trades.Add(CexTrade.FromJson(jsonOrder as JObject));
            }
            trades.Sort(Trade.SortByDate);
            return trades;
        }

        private string UserQuery(string path, Dictionary<string, string> args)
        {
            args["key"] = _publicKey;
            args["nonce"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            args["signature"] = GenerateSignature(args["nonce"]);

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
            var bytes = Encoding.UTF8.GetBytes(string.Format("{0}{1}{2}", nonce, _userId, _publicKey));
            var key = Encoding.ASCII.GetBytes(_secretKey);
            using (var hmac = new HMACSHA256(key))
            {
                byte[] hashmessage = hmac.ComputeHash(bytes);
                return BitConverter.ToString(hashmessage).Replace("-", string.Empty).ToUpper();
            }
        }
    }
}
