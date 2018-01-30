using System;
using System.Globalization;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class Surfer : TradingBot
    {
        private double _minutesToAnalize = 10.0;
        private double _priceChangePercent = 0.0;
        private double _sellPercent = 0.0;

        private IExchangeAnalitics _analitics;

        public Surfer(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        { }

        public override string Name()
        {
            return "Surfer";
        }

        protected override void InitAlgo(XmlNode config)
        {
            if (!(_api is IExchangeAnalitics))
            {
                throw new Exception($"RangerPro can't run on {_api.Name()} exchange, as it doesn't implement IExchangeAnalitics");
            }
            else
                _analitics = _api as IExchangeAnalitics;

            _minutesToAnalize = Convert.ToDouble(config.GetConfigValue("MinutesToAnalize"), CultureInfo.InvariantCulture);
            _priceChangePercent = Convert.ToDouble(config.GetConfigValue("PriceChangePercent"), CultureInfo.InvariantCulture) * 0.01;
            _sellPercent = Convert.ToDouble(config.GetConfigValue("SellPercent"), CultureInfo.InvariantCulture) * 0.01;
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            var funds = GetAlmolstAll(GetLimitedFundsAmount());
            if (funds > _minFundToTrade)
            {
                var coef = CalculatePriceChange(dateTime);
                if (coef >= _priceChangePercent)
                {
                    if (BuyByMarketPrice(funds))
                    {
                        var amount = GetAlmolstAll(GetLimitedBaseAmount());
                        if (amount > _minBaseToTrade)
                        {
                            var order = _api.PlaceOrder(_base, _fund, "sell", amount, _lastPrice + _lastPrice * _sellPercent);
                            NotifyOrderPlaced(order);
                        }
                    }
                }
            }
        }

        private double CalculatePriceChange(DateTime iterationTime)
        {
            var stats = _analitics.GetRecentTrades(_base, _fund, iterationTime.AddMinutes(-1 * _minutesToAnalize));
            stats.Sort(Trade.SortByDate);
            var count = stats.Count;
            var minPrice = double.MaxValue;
            var maxPrice = double.MinValue;
            var midPrice = 0.0;
            var balancedMidPrice = 0.0;
            var sumWeight = 0.0;
            for (int i = 0; i < count; i++)
            {
                var price = stats[i].Price;
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
            var coef = balancedMidPrice / midPrice - 1;
            _log.Info($"Prices for last {_minutesToAnalize} minutes: min={minPrice}; max={maxPrice}; mid={midPrice}; " +
                $"balancedMid={balancedMidPrice}; coef={coef}");

            return coef;
        }

        protected override bool IsStopLoss(Order order, double price)
        {
            var placedPrice = order.Price / (1 + _sellPercent);
            if (price <= placedPrice - placedPrice * _stopLossPercent)
                return true;
            return false;
        }
    }
}
