using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Kuna
{
    public class KunaTrader
    {
        private static int _maxCriticalsCount = 50;
        private static double _minUahToTrade = 5.0;  // Don't place buy orders if UAH funds less than _minUahToTrade
        private static double _minBtcToTrade = 0.0001; // Don't place sell orders if BTC funds less than _minBtcToTrade
        private static TimeSpan _marketOrderFillingTimeout = TimeSpan.FromMinutes(1);
        
        private string _baseUri = "https://kuna.io/api/v2/";
        private string _publicKey;
        private string _secretKey;
        private int _interval = 60;
        private double _sellFarPercent;
        private double _sellNearPercent;
        private double _buyNearPercent;
        private double _buyFarPercent;
        private bool _allowAutoBalanceOrders = false; // refer to description of _imbalanceValue;
        private static double _imbalanceValue = 3.0; // if (BTC*Price)/UAH>_imbaValue or UAH/(BTC*Price) - need to buy or sell to make (BTC*Price)~=UAH

        private int _criticalsCount = 0;

        private Task _tradingTask;

        public bool Trading { get; private set; }

        public void Init()
        {
            var conf = new XmlDocument();
            conf.Load("KunaTraderConfig.xml");
            var node = conf.SelectSingleNode("//KunaTraderConfig/PublicKey");
            _publicKey = node.InnerText;
            node = conf.SelectSingleNode("//KunaTraderConfig/SecretKey");
            _secretKey = node.InnerText;
            node = conf.SelectSingleNode("//KunaTraderConfig/ExecuteInterval");
            _interval = Convert.ToInt32(node.InnerText);
            node = conf.SelectSingleNode("//KunaTraderConfig/SellFarPercent");
            _sellFarPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = conf.SelectSingleNode("//KunaTraderConfig/SellNearPercent");
            _sellNearPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) *0.01;
            node = conf.SelectSingleNode("//KunaTraderConfig/BuyNearPercent");
            _buyNearPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = conf.SelectSingleNode("//KunaTraderConfig/BuyFarPercent");
            _buyFarPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = conf.SelectSingleNode("//KunaTraderConfig/AllowAutoBalanceTrades");
            _allowAutoBalanceOrders = Convert.ToBoolean(node.InnerText);

            Logger.Info("KunaTrader: Inited");
        }

        public Task StartTrading()
        {
            if (Trading)
            {
                Logger.Error("KunaTrader: Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                Logger.Info("KunaTrader: Starting trading...");
                Trading = true;
                while (Trading)
                {
                    DoTradingIteration();
                    Thread.Sleep(_interval * 1000);
                }
            });
            _tradingTask = task;
            return task;
        }

        public void StopTrading()
        {
            Logger.Info("KunaTrader: Stopping trading...");
            Trading = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }

        public void Test()
        {
            //PlaceOrder("sell", 0.0001, 255000.0);
            ////var x = GetMyFunds();
            //var o = GetOrderBook();
            //var res = o.FindPriceForBuy(500);
            //res = o.FindPriceForSell(0.015);
            //var res = SellByMarketPrice(0.0001, 183000);
            //CheckForImbalance(GetLastPrice());
            //var funds = new KunaUserFunds() { Btc = 0.0059306325, Uah = 1133.66910935 };
            //var price = 186787.0;
            //if ((funds.Btc * price) / funds.Uah > _imbalanceValue ||
            //    funds.Uah / (funds.Btc * price) > _imbalanceValue)
            //{
            //}
            //var t = GetMyTrades();
        }

        private void DoTradingIteration()
        {
            try
            {
                var last = GetLastPrice();
                Logger.Info(string.Format("Last price: {0}", last));

                var myOrders = GetMyOrders();
                Logger.Info(string.Format("{0} active orders:", myOrders.Count));
                foreach (var order in myOrders)
                {
                    Logger.Spam(string.Format("Active order: {0}", order));
                }

                MakeDecision(last, myOrders);
            }
            catch (Exception ex)
            {
                Logger.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        private void MakeDecision(double last, List<KunaOrder> myOrders)
        {
            // 0. Not implementing this step so far, but:
            //    If at this stage we have opened "sell-orders" below last price
            //    or "buy-orders" above last price - it means we are currently in progress of
            //    buying or selling currency. It's not desired behavior at this stage (should be done further)
            //    
            //    Let's just write some warnings on this
            foreach (var order in myOrders)
            {
                if (order.Side == "sell" && order.Price < last - last * _buyNearPercent)
                {
                    Logger.Warning(string.Format("Order {0} is below last price"));
                }
                if (order.Side == "buy" && order.Price > last + last * _buyNearPercent)
                {
                    Logger.Warning(string.Format("Order {0} is below last price"));
                }
            }

            // 0.1 Check for imbalance. If we have much more of one currency then another -
            //        current algorithm may be not efficient
            if (myOrders.Count == 0)
            {
                if (!CheckForImbalance(last))
                {
                    // If we are here - there is imbalance, but
                    // no orders with acceptable price to fix this
                    // Skipping iteration so far
                    return;
                }
            }

            // 1. If no orders - do a shot
            if (myOrders.Count == 0)
            {
                DoAShot(last);
                return;
            }

            // 2. If last price is outside orders price range - cancell all and do a shot
            //    It also covers case when currently only 1 order is placed
            if (last < myOrders[0].Price || myOrders.Last().Price < last)
            {
                Logger.Info("Current price outside placed orders range. Cancelling all orders...");
                foreach (var order in myOrders)
                {
                    CancellOrder(order);
                }
                // Commented, as next iteration will do the rest
                //Logger.Info("Sleeping a bit...");
                //Thread.Sleep(2000);
                //DoAShot(last);
                return;
            }

            // 3. If orders count >= 4 - cancell those are farther than 2
            //    Return after this step if exactly 4 orders left after it.
            //    Otherwice we may need to check other cases
            if (myOrders.Count >= 4)
            {
                int mid = 0;
                while (myOrders[mid].Price < last)
                    mid++;

                // Need to cancell orders which have indexes different than [mid-2, mid-1, mid, mid+1]
                var ordersToCancell = new List<KunaOrder>();
                for (int i = 0; i < myOrders.Count; i++)
                {
                    if (Math.Abs(mid - i) > 3)
                    {
                        ordersToCancell.Add(myOrders[i]);
                    }
                }

                foreach (var order in ordersToCancell)
                {
                    Logger.Info(string.Format("Canceling order {0}, as it's far away", order));
                    CancellOrder(order);
                    myOrders.Remove(order);
                }

                if (myOrders.Count == 4)
                {
                    Logger.Info("All good. Waiting for another iteration");
                    return;
                }
            }

            // 4. If orders count == 2 - place missing orders
            //    If we pass step #2 & #3, current price is between these 2 orders
            if (myOrders.Count == 2)
            {
                var funds = GetMyFunds();
                // Place buy order
                if (funds.Uah >= _minUahToTrade)
                {
                    var price = last - ((myOrders[0].Price <= last - last * _buyFarPercent) ?
                        last * _buyNearPercent : last * _buyFarPercent);
                    PlaceOrder("buy", CalculateBuyVolume(price, GetAlmolstAllUah(funds.Uah)), price);
                }
                else
                    Logger.Info(string.Format("Not enought funds to place buy order: {0} UAH", funds.Uah));

                // Place sell order
                if (funds.Btc >= _minBtcToTrade)
                {
                    var price = last + ((myOrders[1].Price >= last + last * _sellFarPercent) ?
                        last * _sellNearPercent : last * _sellFarPercent);
                    PlaceOrder("sell", GetRoundedSellVolume(GetAlmostAllBtc(funds.Btc)), price);
                }
                else
                    Logger.Info(string.Format("Not enough funds to place sell order: {0} BTC", funds.Btc));
            }

            // 5. If orders count == 3 - this will usually mean one of four previously placed
            //    orders was executed. Need to make another decision depending on case
            if (myOrders.Count == 3)
            {
                if (myOrders[0].Side == myOrders[1].Side &&
                    myOrders[1].Side == myOrders[2].Side)
                {
                    foreach (var order in myOrders)
                    {
                        CancellOrder(order);
                    }
                    return;
                }

                MoveHoles(last, myOrders);
            }
        }

        private void MoveHoles(double last, List<KunaOrder> myOrders)
        {
            if (myOrders.Count != 3)
            {
                Logger.Error("MoveHoles: MoveHoles could be called against 3 orders only");
                return;
            }
            // One of below options expected:
            // 1. [far_buy][near_buy]\last/[far_sell] - 
            // 2. [far_b]\last/[near_sell][far_sell] 
            var trades = GetMyTrades();
            if (trades.Count == 0)
            {
                Logger.Error("MoveHoles: No previous orders. Not sure what to do next.. skipping iteration");
                return;
            }

            var lastTrade = trades[0];
            last = lastTrade.Price; // make calculations depending on last trade price, but not last market price

            var placeInvertOrder = false;
            if (myOrders[0].Side == "buy" && myOrders[1].Side == "buy" && myOrders[2].Side == "sell" &&
                myOrders[1].Price < last && last < myOrders[2].Price)
            {
                Logger.Info(@"MoveHoles: Case: [far_buy][near_buy]\last/[far_sell]");
                placeInvertOrder = true;
            }
            else if (myOrders[0].Side == "buy" && myOrders[1].Side == "sell" && myOrders[2].Side == "sell" &&
                myOrders[0].Price < last && last < myOrders[1].Price)
            {
                Logger.Info(@"MoveHoles: Case: [far_buy]\last/[near_sell][far_sell]");
                placeInvertOrder = true;
            }
            else
            {
                Logger.Warning("MoveHoles: Unexpected state. No actions taken ");
            }

            if (placeInvertOrder)
            {
                var funds = GetMyFunds();
                if (lastTrade.Side == "sell")
                {
                    Logger.Info(string.Format("Last trade was: {0}; Placing buy order", lastTrade));
                    if (funds.Uah >= _minUahToTrade)
                    {
                        var price = last - last * _buyNearPercent;
                        PlaceOrder("buy", CalculateBuyVolume(price, GetAlmolstAllUah(funds.Uah)), price);
                    }
                    else
                        Logger.Error(string.Format("Not enough funds to place order: {0}", funds));
                }
                else
                {
                    Logger.Info(string.Format("Last trade was: {0}; Placing sell order", lastTrade));
                    if (funds.Btc >= _minBtcToTrade)
                    {
                        var price = last + last * _sellNearPercent;
                        PlaceOrder("sell", GetRoundedSellVolume(GetAlmostAllBtc(funds.Btc)), price);
                    }
                    else
                        Logger.Error(string.Format("Not enough funds to place order: {0}", funds));
                }
            }
        }


        #region Mad cowboy
        ///// <summary>
        ///// This method proceeds case when we have 3 active orders, and last price inside orders price range
        ///// </summary>
        ///// <param name="last"></param>
        ///// <param name="myOrders"></param>
        //private void MoveHoles(double last, List<KunaOrder> myOrders)
        //{
        //    if (myOrders.Count != 3)
        //    {
        //        Logger.Error("MoveHoles: MoveHoles could be called against 3 orders");
        //        return;
        //    }
        //    // One of below options expected:
        //    // 1. [far_buy][near_buy]\last/[far_sell] - 
        //    // 2. [far_b]\last/[near_sell][far_sell] 

        //    if (myOrders[0].Side == "buy" && myOrders[1].Side == "buy" && myOrders[2].Side == "sell" &&
        //        myOrders[1].Price < last && last < myOrders[2].Price)
        //    {
        //        // Probably price is rising
        //        // If we have UAH - buy BTC
        //        Logger.Info(@"MoveHoles: Case: [far_buy][near_buy]\last/[far_sell]");
        //        var funds = GetMyFunds();
        //        bool successBuy = false;
        //        if (funds.Uah >= _minUahToTrade)
        //        {
        //            // Buy BTC if we can (if market price is not bigger we can then sell)
        //            if (BuyByMarketPrice(funds.Uah - _minUahToTrade / 2.0, last + last * _sellNearPercent))
        //            {
        //                Thread.Sleep(2000);

        //                // Place far sell order
        //                funds = GetMyFunds();
        //                var price = last + last * _sellFarPercent;
        //                PlaceOrder("sell", GetRoundedSellVolume(GetAlmostAllBtc(funds.Btc)), price);
        //                successBuy = true;
        //            }
        //        }
        //        else
        //            Logger.Info(string.Format("Not enought funds to buy BTC by market price. {0}", funds));

        //        // move far buy to near buy
        //        if (successBuy)
        //        {
        //            CancellOrder(myOrders[0]);
        //            Thread.Sleep(2000);
        //            funds = GetMyFunds();
        //            PlaceOrder("buy", CalculateBuyVolume(last - last * _buyNearPercent, GetAlmolstAllUah(funds.Uah)), last - last * _buyNearPercent);
        //        }
        //    }
        //    else if (myOrders[0].Side == "buy" && myOrders[1].Side == "sell" && myOrders[2].Side == "sell" &&
        //        myOrders[0].Price < last && last < myOrders[1].Price)
        //    {
        //        // re-buy
        //        Logger.Info(@"MoveHoles: Case: [far_buy]\last/[near_sell][far_sell]");
        //        var funds = GetMyFunds();
        //        bool successSell = false;
        //        if (funds.Btc >= _minBtcToTrade)
        //        {
        //            // Sell by market price if price is acceptable
        //            if (SellByMarketPrice(funds.Btc - _minBtcToTrade / 2.0, last - last * _buyNearPercent))
        //            {
        //                Thread.Sleep(2000);

        //                // Place far buy order
        //                funds = GetMyFunds();
        //                var price = last - last * _buyFarPercent;
        //                PlaceOrder("buy", CalculateBuyVolume(price, GetAlmolstAllUah(funds.Uah)), price);
        //            }
        //        }
        //        else
        //            Logger.Info(string.Format("Not enought funds to sell BTC by market price. {0}", funds));

        //        // move far sell to near sell
        //        if (successSell)
        //        {
        //            CancellOrder(myOrders[2]);
        //            Thread.Sleep(2000);
        //            funds = GetMyFunds();
        //            PlaceOrder("sell", GetRoundedSellVolume(GetAlmostAllBtc(funds.Btc)), last + last * _sellNearPercent);
        //        }
        //    }
        //    else
        //    {
        //        Logger.Warning("MoveHoles: Unexpected state. No actions taken ");
        //    }
        //}
        #endregion

        private bool SellByMarketPrice(double amountBtc, double minAcceptablePrice)
        {
            var orderBook = GetOrderBook();
            var foundPrice = orderBook.FindPriceForSell(amountBtc);
            if (foundPrice < minAcceptablePrice)
            {
                Logger.Error(string.Format("SellByMarketPrice: OrderBook doesn't have orders to Sell by acceptable price. minAcceptablePrice={0}; foundPrice={1}", minAcceptablePrice, foundPrice));
                return false;
            }

            var sellOrder = PlaceOrder("sell", GetRoundedSellVolume(amountBtc), foundPrice);
            var placedTime = DateTime.Now;
            do
            {
                Thread.Sleep(2000);
                var myOrders = GetMyOrders();
                if (!myOrders.Any(o => o.Id == sellOrder.Id)) // If placed order is not in Active Orders - it filled
                    return true;
            } while (DateTime.Now - placedTime < _marketOrderFillingTimeout);

            Logger.Warning(string.Format("Failed to Sell by market price before timeout. Canceling order {0}", sellOrder));
            CancellOrder(sellOrder);
            return false;
        }

        private bool BuyByMarketPrice(double amountUah, double maxAcceptablePrice)
        {
            var orderBook = GetOrderBook();
            var foundPrice = orderBook.FindPriceForBuy(amountUah);
            if (foundPrice > maxAcceptablePrice)
            {
                Logger.Error(string.Format("BuyByMarketPrice: OrderBook doesn't have orders to Buy by acceptable price. maxAcceptablePrice={0}; foundPrice={1}", maxAcceptablePrice, foundPrice));
                return false;
            }

            var buyOrder = PlaceOrder("buy", CalculateBuyVolume(foundPrice, amountUah), foundPrice);
            var placedTime = DateTime.Now;
            do
            {
                Thread.Sleep(2000);
                var myOrders = GetMyOrders();
                if (!myOrders.Any(o => o.Id == buyOrder.Id))
                    return true;
            } while (DateTime.Now - placedTime < _marketOrderFillingTimeout);

            Logger.Warning(string.Format("Failed to Buy by market price before timeout. Canceling order {0}", buyOrder));
            CancellOrder(buyOrder);
            return false;
        }

        private void DoAShot(double last)
        {
            // Idea: do a shot depending on Mid of OrdersBook, but not on Last Price
            var funds = GetMyFunds();
            if (funds.Btc < _minBtcToTrade || funds.Uah < _minUahToTrade)
            {
                Logger.Error(string.Format("Not enought funds to do a shot: {0}", funds));
                return;
            }

            Logger.Info(string.Format("Doing a shot with funds: {0}", funds));

            var halfUah = (funds.Uah - _minUahToTrade / 2.0) / 2.0; // minus small amaount for easier calculations
            var halfBtc = (funds.Btc - _minBtcToTrade / 2.0) / 2.0; // minus small amaount for easier calculations

            //Logger.Info(string.Format("{0}:{1}:{2}", "sell", GetRoundedSellVolume(halfBtc), last + last * _sellNearPercent));
            //Logger.Info(string.Format("{0}:{1}:{2}", "sell", GetRoundedSellVolume(halfBtc), last + last * _sellFarPercent));
            //Logger.Info(string.Format("{0}:{1}:{2}", "buy", CalculateBuyVolume(last - last * _buyNearPercent, halfUah), last - last * _buyNearPercent));
            //Logger.Info(string.Format("{0}:{1}:{2}", "buy", CalculateBuyVolume(last - last * _buyFarPercent, halfUah), last - last * _buyFarPercent));

            PlaceOrder("sell", GetRoundedSellVolume(halfBtc), last + last * _sellNearPercent);
            Thread.Sleep(250);
            PlaceOrder("sell", GetRoundedSellVolume(halfBtc), last + last * _sellFarPercent);
            Thread.Sleep(250);
            PlaceOrder("buy", CalculateBuyVolume(last - last * _buyNearPercent, halfUah), last - last * _buyNearPercent);
            Thread.Sleep(250);
            PlaceOrder("buy", CalculateBuyVolume(last - last * _buyFarPercent, halfUah), last - last * _buyFarPercent);
            Thread.Sleep(250);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="price"></param>
        /// <returns>False if imbalance, but buy/sell failed for some reason</returns>
        private bool CheckForImbalance(double price)
        {
            var funds = GetMyFunds();
            if (funds.Uah < _minUahToTrade && funds.Btc < _minBtcToTrade)
            {
                // Nothing to do here...
                return true;
            }

            if ((funds.Btc * price) / funds.Uah > _imbalanceValue ||
                funds.Uah / (funds.Btc * price) > _imbalanceValue)
            {
                Logger.Warning(string.Format("Funds imbalance detected: {0}", funds));
                if (!_allowAutoBalanceOrders)
                {
                    Logger.Warning(string.Format("_allowAutoBalanceOrders=false - don't do anything"));
                }
                else
                {
                    if (funds.Btc * price > funds.Uah)
                    {
                        // We have more assets in BTC, need to sell some
                        var sellDiffInUah = (funds.Btc * price - funds.Uah) / 2;
                        var sellAmountBtc = sellDiffInUah / price;
                        if (!SellByMarketPrice(sellAmountBtc, price - price * _buyNearPercent))
                        {
                            Logger.Error(string.Format("CheckForImbalance: Sell by market price failed. Amount: {0}BTC", sellAmountBtc));
                            return false;
                        }
                    }
                    else
                    {
                        // We have more assets in UAH, need to buy some BTC
                        var buyDiffInUah = (funds.Uah - funds.Btc * price) / 2;
                        if (!BuyByMarketPrice(buyDiffInUah, price + price * _sellNearPercent))
                        {
                            Logger.Error(string.Format("CheckForImbalance: Buy by market price failed. Amount: {0}UAH", buyDiffInUah));
                            return false;
                        }
                    }
                }
            }
            return true;
        }


        private double GetLastPrice()
        {
            var tickerResponse = WebApi.Query("https://kuna.io/api/v2/tickers/btcuah");
            Logger.Spam("Response: " + tickerResponse);
            var json = JObject.Parse(tickerResponse);
            var last = (JValue)json["ticker"]["last"];
            return Convert.ToDouble(last.Value, CultureInfo.InvariantCulture);
        }

        private KunaOrderBook GetOrderBook()
        {
            var response = WebApi.Query("https://kuna.io/api/v2/order_book?market=btcuah");
            Logger.Spam("GetOrderBoor: response: " + response);
            return KunaOrderBook.FromJson(JObject.Parse(response));
        }

        private KunaOrder PlaceOrder(string side, double volume, double price)
        {
            Logger.Spam(string.Format("Placing order: {0}:{1}:{2}", side, volume, price));
            if (volume == 0 || price == 0)
                throw new Exception(string.Format("PlaceOrder: Volume or Price can't be zero. Vol={0}; Price={1}", volume, price));

            var response = UserQuery("orders", "POST", new Dictionary<string, string>(){
                { "side", side },
                { "volume", volume.ToString(CultureInfo.InvariantCulture) },
                { "market", "btcuah" },
                { "price", price.ToString(CultureInfo.InvariantCulture) }
            });
            Logger.Spam("Response: " + response);

            // Response of placing order is order JSON
            // Checking if query was successfull by trying to parse response as order
            var resp = KunaOrder.FromJson(JObject.Parse(response));
            Logger.Warning(string.Format("KunaTrading: Order {0} placed", resp));
            return resp;
        }

        private void CancellOrder(KunaOrder order)
        {
            var response = UserQuery("order/delete", "POST", new Dictionary<string, string>()
                { { "id", order.Id.ToString() } });
            Logger.Spam("Response: " + response);

            // Response of cancellation is order JSON
            // Checking if query was successfull by trying to parse response as order
            var resp = KunaOrder.FromJson(JObject.Parse(response));
            Logger.Warning(string.Format("KunaTrading: Order {0} was cancelled", resp));
        }

        private KunaUserFunds GetMyFunds()
        {
            var response = UserQuery("members/me", "GET", new Dictionary<string, string>());
            Logger.Spam("Response: " + response);
            var json = JObject.Parse(response);
            return KunaUserFunds.FromUserInfo(json);
        }

        /// <summary>
        /// Returns user's orders sorted by price
        /// </summary>
        /// <returns></returns>
        private List<KunaOrder> GetMyOrders()
        {
            var orders = new List<KunaOrder>();
            var response = UserQuery("orders", "GET", new Dictionary<string, string>() { { "market", "btcuah" } });
            Logger.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                orders.Add(KunaOrder.FromJson(jsonOrder as JObject));
            }
            orders.Sort(KunaOrder.SortByPrice);
            return orders;
        }

        /// <summary>
        /// Returns user's recent trades sorted by date (desc)
        /// </summary>
        /// <returns></returns>
        private List<KunaTrade> GetMyTrades()
        {
            var trades = new List<KunaTrade>();
            var response = UserQuery("trades/my", "GET", new Dictionary<string, string>() { { "market", "btcuah" } });
            Logger.Spam("Response: " + response);

            var json = JArray.Parse(response);
            foreach (var jsonOrder in json)
            {
                trades.Add(KunaTrade.FromJson(jsonOrder as JObject));
            }
            trades.Sort(KunaTrade.SortByDate);
            return trades;
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
            Logger.Spam(string.Format("{0}: {1}{2}?{3}", method, _baseUri, path, dataStr));

            if (method == "POST")
            {
                var request = WebRequest.Create(new Uri(_baseUri + path + "?" + dataStr)) as HttpWebRequest;
                if (request == null)
                    throw new Exception("Non HTTP WebRequest: " + _baseUri + path);
                
                //var data = Encoding.ASCII.GetBytes(dataStr);
                request.Method = method;
                request.Timeout = 15000;
                request.ContentType = "application/x-www-form-urlencoded";
                //request.ContentLength = data.Length;
                //var reqStream = request.GetRequestStream();
                //reqStream.Write(data, 0, data.Length);
                //reqStream.Close();
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
        
        private static double CalculateBuyVolume(double price, double uah)
        {
            return Math.Round(Math.Floor(uah) / price, 5);
        }

        private static double GetRoundedSellVolume(double btc)
        {
            return Math.Round(btc, 5);
        }

        private static double GetAlmolstAllUah(double uah)
        {
            return uah - _minUahToTrade / 2.0;
        }

        private static double GetAlmostAllBtc(double btc)
        {
            return btc - _minBtcToTrade / 2.0;
        }
        
        /// <summary>
        /// Kind of protection
        /// </summary>
        private void AddCritical()
        {
            _criticalsCount++;
            if (_criticalsCount >= _maxCriticalsCount)
            {
                Logger.Critical("KunaTrading: Criticals count exceeded maximum. Something goes wrong. Will stop trading");
                StopTrading();
            }
        }
    }
}
