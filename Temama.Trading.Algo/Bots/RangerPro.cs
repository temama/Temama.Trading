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
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class RangerPro: TradingBot
    {
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

        public RangerPro(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        { }

        protected override void InitAlgo(XmlNode config)
        {
            if (!(_api is IExchangeAnalitics))
            {
                throw new Exception($"RangerPro can't run on {_api.Name()} exchange, as it doesn't implement IExchangeAnalitics");
            }
            else
                _analitics = _api as IExchangeAnalitics;
            
            _hoursToAnalyze = Convert.ToInt32(config.GetConfigValue("HoursToAnalyze"), CultureInfo.InvariantCulture);
            var corrInterval = config.GetConfigValue("RangeCorrectionInterval");
            if (corrInterval.ToLower() == "attrade")
                _correctRangeEachTrade = true;
            else
                _rangeCorrectionInterval = TimeSpan.FromSeconds(Convert.ToInt32(corrInterval));
            
            _percentToSell = Convert.ToDouble(config.GetConfigValue("SellPercent"), CultureInfo.InvariantCulture) * 0.01;
            _percentToBuy = Convert.ToDouble(config.GetConfigValue("BuyPercent"), CultureInfo.InvariantCulture) * 0.01;
            
            _allowSellCancel = Convert.ToBoolean(config.GetConfigValue("AllowSellCancel"));
            _sellCancelHours = Convert.ToDouble(config.GetConfigValue("SellCancelHours"), CultureInfo.InvariantCulture);
            _sellCancelDistancePercent = Convert.ToDouble(config.GetConfigValue("SellCancelDistancePercent"), CultureInfo.InvariantCulture) * 0.01;
            
            _allowBuyCancel = Convert.ToBoolean(config.GetConfigValue("AllowBuyCancel"));
            _buyCancelHours = Convert.ToDouble(config.GetConfigValue("BuyCancelHours"), CultureInfo.InvariantCulture);
            _buyCancelDistancePercent = Convert.ToDouble(config.GetConfigValue("BuyCancelDistancePercent"), CultureInfo.InvariantCulture) * 0.01;
        }
        
        protected override void TradingIteration(DateTime iterationTime)
        {
            if (iterationTime - _lastRangeCorrectionTime > _rangeCorrectionInterval)
            {
                var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                if (!_correctRangeEachTrade)
                    CorrectRange(stats, iterationTime);
                CancelFarAwayOrders(stats, iterationTime);
                _lastRangeCorrectionTime = iterationTime;
            }

            var funds = _api.GetFunds(_base, _fund);

            if (funds.Values[_base] > _minBaseToTrade)
            {
                _log.Info("RangerPro: Can place sell order...");

                if (_correctRangeEachTrade)
                {
                    var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                    CorrectRange(stats, iterationTime);
                }

                var order = _api.PlaceOrder(_base, _fund, "sell", 
                    _api.GetRoundedSellVolume(GetAlmolstAll(funds.Values[_base])), _priceToSell);
                NotifyOrderPlaced(order);
            }

            if (funds.Values[_fund] > _minFundToTrade)
            {
                if (_correctRangeEachTrade)
                {
                    var stats = _analitics.GetRecentPrices(_base, _fund, iterationTime.AddHours(-1 * _hoursToAnalyze));
                    CorrectRange(stats, iterationTime);
                }

                var amount = _api.CalculateBuyVolume(_priceToBuy, GetAlmolstAll(funds.Values[_fund]));
                if (amount > _minBaseToTrade)
                {
                    _log.Info("RangerPro: Can place buy order...");
                    var order = _api.PlaceOrder(_base, _fund, "buy", amount, _priceToBuy);
                    NotifyOrderPlaced(order);
                }
            }
        }
        
        private void CorrectRange(List<Tick> stats, DateTime iterationTime)
        {
            _log.Info("RangerPro: Range correction...");

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
            _log.Info(string.Format("Prices for last {0} hours: min={1}; max={2}; mid={3}; balancedMid={4}",
                _hoursToAnalyze, minPrice, maxPrice, midPrice, balancedMidPrice));

            _priceToBuy = Math.Round(balancedMidPrice - balancedMidPrice * _percentToBuy, 4);
            _priceToSell = Math.Round(balancedMidPrice + balancedMidPrice * _percentToSell, 4);

            var msg = $"Range was corrected to: Buy={_priceToBuy}; Sell={_priceToSell}";
            _log.Info("!!! " + msg);
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
                    _log.Info(string.Format("WARN: for last {0} hours closest price diff with [{1}] order was:{2}",
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
                            _log.Important(string.Format("Cancel order as far away: {0}", order));
                            _api.CancellOrder(order);
                            NotifyOrderCancel(order);
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
                            _log.Important(string.Format("Cancel order as far away: {0}", order));
                            _api.CancellOrder(order);
                            NotifyOrderCancel(order);
                        }
                    }
                }
            }
        }
    }
}
