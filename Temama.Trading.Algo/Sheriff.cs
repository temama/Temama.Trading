using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;

namespace Temama.Trading.Algo
{
    public class Sheriff : Algorithm
    {
        private double _imbalanceValue;

        private int _interval = 60;
        private double _sellFarPercent;
        private double _sellNearPercent;
        private double _buyNearPercent;
        private double _buyFarPercent;
        private bool _allowAutoBalanceOrders = false; // refer to description of _imbalanceValue;       

        public override void Init(IExchangeApi api, XmlDocument config)
        {
            _api = api;

            var node = config.SelectSingleNode("//TemamaTradingConfig/BaseCurrency");
            _base = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/FundCurrency");
            _fund = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/MinBaseToTrade");
            _minBaseToTrade = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/MinFundToTrade");
            _minFundToTrade = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/ExecuteInterval");
            _interval = Convert.ToInt32(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/SellFarPercent");
            _sellFarPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/SellNearPercent");
            _sellNearPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyNearPercent");
            _buyNearPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyFarPercent");
            _buyFarPercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/AllowAutoBalanceTrades");
            _allowAutoBalanceOrders = Convert.ToBoolean(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/ImbalanceValue");
            _imbalanceValue = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);

            _pair = _base + _fund;
        }

        public override Task StartTrading()
        {
            if (Trading)
            {
                Logger.Error("Sheriff: Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                Logger.Info(string.Format("Sheriff: Starting trading pair {0}...", _pair.ToUpper()));
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

        public override void StopTrading()
        {
            Logger.Info("Sheriff: Stopping trading...");
            Trading = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }

        private void DoTradingIteration()
        {
            try
            {
                var last = _api.GetLastPrice(_base, _fund);
                Logger.Info(string.Format("Last price: {0}", last));

                var myOrders = _api.GetMyOrders(_base, _fund);
                var sbOrders = new StringBuilder();
                foreach (var order in myOrders)
                {
                    Logger.Spam(string.Format("Active order: {0}", order));
                    sbOrders.Append(string.Format("{0}:{1}({2}); ", order.Side == "sell" ? "s" : "b", order.Price, order.Volume));
                }
                Logger.Info(string.Format("{0} active orders: {1}", myOrders.Count, sbOrders));

                if (DateTime.Now - _lastFiatBalanceCheckTime > _FiatBalanceCheckInterval)
                {
                    CheckFiatBalance(last, myOrders);
                }

                MakeDecision(last, myOrders);
            }
            catch (Exception ex)
            {
                Logger.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        private void MakeDecision(double last, List<Order> myOrders)
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
                    _api.CancellOrder(order);
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
                var ordersToCancell = new List<Order>();
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
                    _api.CancellOrder(order);
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
                var funds = _api.GetFunds(_base, _fund);
                // Place buy order
                if (funds.Values[_fund] >= _minFundToTrade)
                {
                    var price = last - ((myOrders[0].Price <= last - last * _buyFarPercent) ?
                        last * _buyNearPercent : last * _buyFarPercent);
                    _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(price, GetAlmolstAllFunds(funds.Values[_fund])), price);
                }
                else
                    Logger.Info(string.Format("Not enought funds to place buy order: {0} UAH", funds.Values[_fund]));

                // Place sell order
                if (funds.Values[_base] >= _minBaseToTrade)
                {
                    var price = last + ((myOrders[1].Price >= last + last * _sellFarPercent) ?
                        last * _sellNearPercent : last * _sellFarPercent);
                    _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(GetAlmostAllBases(funds.Values[_base])), price);
                }
                else
                    Logger.Info(string.Format("Not enough funds to place sell order: {0} BTC", funds.Values[_base]));
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
                        _api.CancellOrder(order);
                    }
                    return;
                }

                MoveHoles(last, myOrders);
            }
        }

        private void MoveHoles(double last, List<Order> myOrders)
        {
            if (myOrders.Count != 3)
            {
                Logger.Error("MoveHoles: MoveHoles could be called against 3 orders only");
                return;
            }
            // One of below options expected:
            // 1. [far_buy][near_buy]\last/[far_sell] - 
            // 2. [far_b]\last/[near_sell][far_sell] 
            var trades = _api.GetMyTrades(_base, _fund);
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
                var funds = _api.GetFunds(_base, _fund);
                if (lastTrade.Side == "sell")
                {
                    Logger.Info(string.Format("Last trade was: {0}; Placing buy order", lastTrade));
                    if (funds.Values[_fund] >= _minFundToTrade)
                    {
                        var price = last - last * _buyNearPercent;
                        _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(price, GetAlmolstAllFunds(funds.Values[_fund])), price);
                    }
                    else
                        Logger.Error(string.Format("Not enough funds to place order: {0}", funds));
                }
                else
                {
                    Logger.Info(string.Format("Last trade was: {0}; Placing sell order", lastTrade));
                    if (funds.Values[_base] >= _minBaseToTrade)
                    {
                        var price = last + last * _sellNearPercent;
                        _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(GetAlmostAllBases(funds.Values[_base])), price);
                    }
                    else
                        Logger.Error(string.Format("Not enough funds to place order: {0}", funds));
                }
            }
        }

        private void DoAShot(double last)
        {
            // Idea: do a shot depending on Mid of OrdersBook, but not on Last Price
            var funds = _api.GetFunds(_base, _fund);
            if (funds.Values[_base] < _minBaseToTrade || funds.Values[_fund] < _minFundToTrade)
            {
                Logger.Error(string.Format("Not enought funds to do a shot: {0}", funds));
                return;
            }

            Logger.Info(string.Format("Doing a shot with funds: {0}", funds));

            var halfUah = (funds.Values[_fund] - _minFundToTrade / 2.0) / 2.0; // minus small amaount for easier calculations
            var halfBtc = (funds.Values[_base] - _minBaseToTrade / 2.0) / 2.0; // minus small amaount for easier calculations
            
            _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(halfBtc), last + last * _sellNearPercent);
            Thread.Sleep(250);
            _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(halfBtc), last + last * _sellFarPercent);
            Thread.Sleep(250);
            _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(last - last * _buyNearPercent, halfUah), last - last * _buyNearPercent);
            Thread.Sleep(250);
            _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(last - last * _buyFarPercent, halfUah), last - last * _buyFarPercent);
            Thread.Sleep(250);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="price"></param>
        /// <returns>False if imbalance, but buy/sell failed for some reason</returns>
        private bool CheckForImbalance(double price)
        {
            var funds = _api.GetFunds(_base, _fund);
            var fBase = funds.Values[_base];
            var fFund = funds.Values[_fund];
            if (fFund < _minFundToTrade && fBase < _minBaseToTrade)
            {
                // Nothing to do here...
                return true;
            }

            if ((fBase * price) / fFund > _imbalanceValue ||
                fFund / (fBase * price) > _imbalanceValue)
            {
                Logger.Warning(string.Format("Funds imbalance detected: {0}", funds));
                if (!_allowAutoBalanceOrders)
                {
                    Logger.Warning(string.Format("_allowAutoBalanceOrders=false - don't do anything"));
                }
                else
                {
                    if (fBase * price > fFund)
                    {
                        // We have more assets in BTC, need to sell some
                        var sellDiffInUah = (fBase * price - fFund) / 2;
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
                        var buyDiffInUah = (fFund - fBase * price) / 2;
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

        private bool SellByMarketPrice(double amountBtc, double minAcceptablePrice)
        {
            var orderBook = _api.GetOrderBook(_base, _fund);
            var foundPrice = orderBook.FindPriceForSell(amountBtc);
            if (foundPrice < minAcceptablePrice)
            {
                Logger.Error(string.Format("SellByMarketPrice: OrderBook doesn't have orders to Sell by acceptable price. minAcceptablePrice={0}; foundPrice={1}", minAcceptablePrice, foundPrice));
                return false;
            }

            var sellOrder = _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(amountBtc), foundPrice);
            var placedTime = DateTime.Now;
            do
            {
                Thread.Sleep(2000);
                var myOrders = _api.GetMyOrders(_base, _fund);
                if (!myOrders.Any(o => o.Id == sellOrder.Id)) // If placed order is not in Active Orders - it filled
                    return true;
            } while (DateTime.Now - placedTime < _marketOrderFillingTimeout);

            Logger.Warning(string.Format("Failed to Sell by market price before timeout. Canceling order {0}", sellOrder));
            _api.CancellOrder(sellOrder);
            return false;
        }

        private bool BuyByMarketPrice(double amountUah, double maxAcceptablePrice)
        {
            var orderBook = _api.GetOrderBook(_base, _fund);
            var foundPrice = orderBook.FindPriceForBuy(amountUah);
            if (foundPrice > maxAcceptablePrice)
            {
                Logger.Error(string.Format("BuyByMarketPrice: OrderBook doesn't have orders to Buy by acceptable price. maxAcceptablePrice={0}; foundPrice={1}", maxAcceptablePrice, foundPrice));
                return false;
            }

            var buyOrder = _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(foundPrice, amountUah), foundPrice);
            var placedTime = DateTime.Now;
            do
            {
                Thread.Sleep(2000);
                var myOrders = _api.GetMyOrders(_base, _fund);
                if (!myOrders.Any(o => o.Id == buyOrder.Id))
                    return true;
            } while (DateTime.Now - placedTime < _marketOrderFillingTimeout);

            Logger.Warning(string.Format("Failed to Buy by market price before timeout. Canceling order {0}", buyOrder));
            _api.CancellOrder(buyOrder);
            return false;
        }
    }
}
