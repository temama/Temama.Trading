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
            node = config.SelectSingleNode("//TemamaTradingConfig/SellPercent");
            _percentToSell = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/BuyPercent");
            _percentToBuy = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;

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
                    DoTradingIteration();
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

        private void DoTradingIteration()
        {
            try
            {
                if (DateTime.Now - _lastRangeCorrectionTime > _rangeCorrectionInterval)
                {
                    CorrectRange();
                    _lastRangeCorrectionTime = DateTime.Now;
                }

                PrintSummary();
                
                MakeDecision();
            }
            catch (Exception ex)
            {
                Logger.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        private void PrintSummary()
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
        }

        private void CorrectRange()
        {
            Logger.Info("RangerPro: Range correction...");

            var stats = _analitics.GetRecentPrices(_base, _fund, DateTime.UtcNow.AddHours(-1 * _hoursToAnalyze));
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

            Logger.Info(string.Format("!!! Range was corrected to: Buy={0}; Sell={1} !!!", _priceToBuy, _priceToSell));
        }

        private int SortTickAscByDateTime(Tick first, Tick second)
        {
            if (first.Time < second.Time)
                return -1;
            else if (second.Time < first.Time)
                return 1;
            else return 0;
        }

        private void MakeDecision()
        {
            var funds = _api.GetFunds(_base, _fund);

            if (funds.Values[_base] > _minBaseToTrade)
            {
                Logger.Info("RangerPro: Can place sell order...");
                _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(GetAlmostAllBases(funds.Values[_base])), _priceToSell);
            }

            if (funds.Values[_fund] > _minFundToTrade)
            {
                Logger.Info("RangerPro: Can place buy order...");
                _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(_priceToBuy, GetAlmolstAllFunds(funds.Values[_fund])), _priceToBuy);
            }
        }

    }
}
