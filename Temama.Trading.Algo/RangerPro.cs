using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;

namespace Temama.Trading.Algo
{
    public class RangerPro: Algorithm
    {
        private int _interval = 60;
        private double _priceToSell;
        private double _priceToBuy;
        private double _percentToBuy;
        private double _percentToSell;
        private int _hoursToAnalyze;
        private IExchangeAnalitics _analitics;
        private DateTime _lastRangeCorrectionTime = DateTime.MinValue;
        private TimeSpan _rangeCorrectionInterval = TimeSpan.FromMinutes(30);
        private bool _correctRangeEachTrade = false;

        private bool _allowSellCancel = false;
        private double _sellCancelHours = 0;
        private double _sellCancelDistancePercent = 0.0;
        private bool _allowBuyCancel = false;
        private double _buyCancelHours = 0;
        private double _buyCancelDistancePercent = 0.0;

        public override string Name()
        {
            return "RangerPRO";
        }

        public override void Init(IExchangeApi api, XmlDocument config)
        {
            if (!(api is IExchangeAnalitics))
            {
                throw new Exception(string.Format("RangerPro can't run on {0} exchange, as it doesn't implement IExchangeAnalitics",
                    api.Name()));
            }
            else
                _analitics = api as IExchangeAnalitics;

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
            node = config.SelectSingleNode("//TemamaTradingConfig/HoursToAnalyze");
            _hoursToAnalyze = Convert.ToInt32(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/RangeCorrectionInterval");
            if (node.InnerText.ToLower() == "attrade")
                _correctRangeEachTrade = true;
            else
                _rangeCorrectionInterval = TimeSpan.FromSeconds(Convert.ToInt32(node.InnerText));
            node = config.SelectSingleNode("//TemamaTradingConfig/SellPercent");
            _percentToSell = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyPercent");
            _percentToBuy = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;

            node = config.SelectSingleNode("//TemamaTradingConfig/AllowSellCancel");
            _allowSellCancel = Convert.ToBoolean(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/SellCancelHours");
            _sellCancelHours = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/SellCancelDistancePercent");
            _sellCancelDistancePercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/AllowBuyCancel");
            _allowBuyCancel = Convert.ToBoolean(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyCancelHours");
            _buyCancelHours = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyCancelDistancePercent");
            _buyCancelDistancePercent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;

            _pair = _base + "/" + _fund;
        }

        public void Test()
        {
            //var t = _api.GetMyTrades(_base, _fund);
        }

        public override Task StartTrading()
        {
            if (Trading)
            {
                Logger.Error("RangerPro: Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                Logger.Info(string.Format("RangerPro: Starting trading pair {0}...", _pair.ToUpper()));
                Trading = true;
                while (Trading)
                {
                    DoTradingIteration(DateTime.Now);
                    Thread.Sleep(_interval * 1000);
                }
            });
            _tradingTask = task;
            return task;
        }

        public override void StopTrading()
        {
            Logger.Info("RangerPro: Stopping trading...");
            Trading = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }

        public override void Emulate(DateTime start, DateTime end)
        {
            var emu = _api as IExchangeEmulator;
            var currTime = start;
            emu.SetIterationTime(currTime);
            Trading = true;
            while (currTime <= end && Trading)
            {
                Logger.Info("RangerPro.Emulation: Iter Time: " + currTime);
                emu.SetIterationTime(currTime);
                DoTradingIteration(currTime);
                currTime = currTime.AddSeconds(_interval);
            }
        }

        private void DoTradingIteration(DateTime iterationTime)
        {
            try
            {
                if (iterationTime - _lastRangeCorrectionTime > _rangeCorrectionInterval)
                {
                    var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                    CorrectRange(stats, iterationTime);
                    CancelFarAwayOrders(stats, iterationTime);
                    _lastRangeCorrectionTime = iterationTime;
                }

                PrintSummary(iterationTime);
                
                MakeDecision(iterationTime);
            }
            catch (Exception ex)
            {
                Logger.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        private void PrintSummary(DateTime iterationTime)
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

            if (iterationTime - _lastFiatBalanceCheckTime > _FiatBalanceCheckInterval)
            {
                CheckFiatBalance(last, myOrders);
                _lastFiatBalanceCheckTime = iterationTime;
            }
        }

        private void CorrectRange(List<Tick> stats, DateTime iterationTime)
        {
            Logger.Info("RangerPro: Range correction...");

            stats.Sort(SortTickAscByDateTime);
            var count = stats.Count;
            var minPrice = double.MaxValue;
            var maxPrice = double.MinValue;
            var midPrice = 0.0;
            var balancedMidPrice = 0.0;
            var sumWeight = 0.0;
            for (int i = 0; i < count; i++)
            {
                var price = stats[i].Last;
                if (price < minPrice)
                    minPrice = price;
                if (price > maxPrice)
                    maxPrice = price;

                var weight = ((double)(i + 1)) / (double)count;
                balancedMidPrice += price * weight;
                sumWeight += weight;
            }
            midPrice = minPrice + (maxPrice - minPrice) / 2.0;
            balancedMidPrice /= sumWeight;
            Logger.Info(string.Format("Prices for last {0} hours: min={1}; max={2}; mid={3}; balancedMid={4}",
                _hoursToAnalyze, minPrice, maxPrice, midPrice, balancedMidPrice));

            _priceToBuy = Math.Round(balancedMidPrice - balancedMidPrice * _percentToBuy, 4);
            _priceToSell = Math.Round(balancedMidPrice + balancedMidPrice * _percentToSell, 4);

            var msg = string.Format("Range was corrected to: Buy={0}; Sell={1}", _priceToBuy, _priceToSell);
            Logger.Info("!!! " + msg);
            NotificationManager.SendInfo(WhoAmI, msg);

            var orders = _api.GetMyOrders(_base, _fund);
            foreach (var ord in orders)
            {
                if ((iterationTime - ord.CreatedAt).TotalHours >= _hoursToAnalyze)
                {
                    var closest = double.MaxValue;
                    foreach (var tick in stats)
                    {
                        if (Math.Abs(ord.Price - tick.Last) < closest)
                            closest = Math.Abs(ord.Price - tick.Last);
                    }
                    Logger.Info(string.Format("WARN: for last {0} hours closest price diff with [{1}] order was:{2}",
                        _hoursToAnalyze, ord, closest));
                }
            }
        }
        
        private int SortTickAscByDateTime(Tick first, Tick second)
        {
            if (first.Time < second.Time)
                return -1;
            else if (second.Time < first.Time)
                return 1;
            else return 0;
        }

        private void MakeDecision(DateTime iterationTime)
        {
            var funds = _api.GetFunds(_base, _fund);

            if (funds.Values[_base] > _minBaseToTrade)
            {
                Logger.Info("RangerPro: Can place sell order...");
                
                if (_correctRangeEachTrade)
                {
                    var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                    CorrectRange(stats, iterationTime);
                }

                var order = _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(GetAlmostAllBases(funds.Values[_base])), _priceToSell);
                NotificationManager.SendImportant(WhoAmI, string.Format("Order placed: {0}", order));
            }

            if (funds.Values[_fund] > _minFundToTrade)
            {
                if (_correctRangeEachTrade)
                {
                    var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                    CorrectRange(stats, iterationTime);
                }

                var amount = CalculateBuyVolume(_priceToBuy, GetAlmolstAllFunds(funds.Values[_fund]));
                if (amount > _minBaseToTrade)
                {
                    Logger.Info("RangerPro: Can place buy order...");

                    var order = _api.PlaceOrder(_base, _fund, "buy", amount, _priceToBuy);
                    NotificationManager.SendImportant(WhoAmI, string.Format("Order placed: {0}", order));
                }
            }
        }

        private void CancelFarAwayOrders(List<Tick> stats, DateTime iterationTime)
        {
            var orders = _api.GetMyOrders(_base, _fund);
            var last = _api.GetLastPrice(_base, _fund);

            if (_allowSellCancel)
            {
                foreach (var order in orders)
                {
                    if (order.Side == "sell" &&
                        (iterationTime - order.CreatedAt).TotalHours > _sellCancelHours)
                    {
                        var needToCancel = true;
                        var allowedPrice = order.Price - order.Price * _sellCancelDistancePercent;

                        foreach(var tick in stats)
                        {
                            if (tick.Last > allowedPrice)
                            {
                                needToCancel = false;
                                break;
                            }
                        }

                        if (needToCancel)
                        {
                            Logger.Important(string.Format("Cancel order as far away: {0}", order));
                            _api.CancellOrder(order);
                        }
                    }
                }
            }

            if (_allowBuyCancel)
            {
                foreach (var order in orders)
                {
                    if (order.Side == "buy" &&
                        (iterationTime - order.CreatedAt).TotalHours > _buyCancelHours)
                    {
                        var needToCancel = true;
                        var allowedPrice = order.Price + order.Price * _buyCancelDistancePercent;

                        foreach (var tick in stats)
                        {
                            if (tick.Last < allowedPrice)
                            {
                                needToCancel = false;
                                break;
                            }
                        }

                        if (needToCancel)
                        {
                            Logger.Important(string.Format("Cancel order as far away: {0}", order));
                            _api.CancellOrder(order);
                        }
                    }
                }
            }
        }
    }
}
