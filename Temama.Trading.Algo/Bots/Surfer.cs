using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Common;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class Surfer : TradingBot
    {
        private List<Signal> _signals = new List<Signal>();
        private double _candleWidth = 10.0;
        private double _takeProfit = 1.0;
        private double _zeroTolerance = 0.0001;
        private int _minSignalCandlesCount = int.MaxValue;
        private int _maxSignalCandlesCount = 0;

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

            _candleWidth = Convert.ToDouble(config.GetConfigValue("CandlestickWidth"), CultureInfo.InvariantCulture);
            _zeroTolerance = Convert.ToDouble(config.GetConfigValue("ZeroTolerance", true, "0.0001"), CultureInfo.InvariantCulture) * 0.01;
            _takeProfit = Convert.ToDouble(config.GetConfigValue("TakeProfit"), CultureInfo.InvariantCulture) * 0.01;

            _signals = new List<Signal>();
            var signals = config.SelectNodes("Signals/Signal");
            foreach (XmlNode signalNode in signals)
            {
                var signal = Signal.Parse(signalNode);
                if (signal.CandlesCount > _maxSignalCandlesCount)
                    _maxSignalCandlesCount = signal.CandlesCount;
                if (signal.CandlesCount < _minSignalCandlesCount)
                    _minSignalCandlesCount = signal.CandlesCount;
                signal.ZeroTolerance = _zeroTolerance;

                _signals.Add(signal);
            }

            if (_signals.Count == 0)
                throw new Exception("At least one signal should be provided at config");

            if (_pricePersistInterval == 0)
            {
                _pricePersistInterval = (int)_candleWidth * (_maxSignalCandlesCount + 1);
            }
            _analitics.SetHistoricalTradesPersistInterval(_base, _fund, TimeSpan.FromMinutes(_pricePersistInterval));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            var funds = GetAlmolstAll(GetLimitedFundsAmount());
            if (funds > _minFundToTrade)
            {
                var signal = CheckSignals(dateTime);
                if (signal != null)
                {
                    _log.Important($"{signal.SignalName} pattern found at {dateTime}");
                    if (BuyByMarketPrice(funds))
                    {
                        var amount = GetAlmolstAll(GetLimitedBaseAmount());
                        if (amount > _minBaseToTrade)
                        {
                            var price = signal.LastPriceExpectation > 0 ? 
                                signal.LastPriceExpectation : _lastPrice + _lastPrice * _takeProfit;
                            
                            var order = _api.PlaceOrder(_base, _fund, "sell", amount, price);
                            NotifyOrderPlaced(order);
                        }
                        else
                            _log.Warning($"Not enough {_base} to place sell order");
                    }
                }
            }
        }

        //private double CalculatePriceChange(DateTime iterationTime)
        //{
        //    var stats = _analitics.GetRecentTrades(_base, _fund, iterationTime.AddMinutes(-1 * _minutesToAnalize));
        //    stats.Sort(Trade.SortByDate);
        //    var count = stats.Count;
        //    var minPrice = double.MaxValue;
        //    var maxPrice = double.MinValue;
        //    var midPrice = 0.0;
        //    var balancedMidPrice = 0.0;
        //    var sumWeight = 0.0;
        //    for (int i = 0; i < count; i++)
        //    {
        //        var price = stats[i].Price;
        //        if (price < minPrice)
        //            minPrice = price;
        //        if (price > maxPrice)
        //            maxPrice = price;

        //        var weight = ((double)(i + 1)) / (double)count;
        //        balancedMidPrice += price * weight;
        //        sumWeight += weight;
        //    }
        //    midPrice = minPrice + (maxPrice - minPrice) / 2.0;
        //    balancedMidPrice /= sumWeight;
        //    var coef = balancedMidPrice / midPrice - 1;
        //    _log.Info($"Prices for last {_minutesToAnalize} minutes: min={minPrice}; max={maxPrice}; mid={midPrice}; " +
        //        $"balancedMid={balancedMidPrice}; coef={coef}");

        //    return coef;
        //}

        //private double CalculatePriceChange(DateTime iterationTime)
        //{
        //    var stats = _analitics.GetRecentTrades(_base, _fund, iterationTime.AddMinutes(-1 * _minutesToAnalize));
        //    var candles = CandlestickHelper.TradesToCandles(stats, TimeSpan.FromMinutes(_minutesToAnalize / 4));

        //    _log.Spam($"Candlesticks for last {_minutesToAnalize} minutes:");
        //    foreach (var candle in candles)
        //    {
        //        _log.Spam(candle.ToString());
        //    }

        //    if (candles.Count < 3)
        //        return 0;

        //    var c1 = candles[candles.Count - 3];
        //    var c2 = candles[candles.Count - 2];
        //    var c3 = candles[candles.Count - 1];

        //    if (!(c1.Green && c2.Green && c3.Green))
        //        return 0;

        //    if ((c1.Volume == 0 && c2.Volume == 0) ||
        //        (c2.Volume == 0 && c3.Volume == 0))
        //        return 0;

        //    if (c2.Volume > c1.Volume - _volumeTolerance &&
        //        c3.Volume > c2.Volume - _volumeTolerance)
        //    {
        //        return c3.Close / c1.Open - 1;
        //    }
        //    else
        //        return 0;
        //}

        private Signal CheckSignals(DateTime iterationTime)
        {
            var time = iterationTime.ToUniversalTime();
            // TODO: Convert everything to UTC
            if (!_analitics.HasHistoricalDataStartingFrom(_base, _fund,
                time.AddMinutes(-1 * _minSignalCandlesCount * _candleWidth), true))
            {
                _log.Info("Not enough historical data to perform iteration");
                return null;
            }

            var stats = _analitics.GetRecentTrades(_base, _fund, time.AddMinutes(-1 * _pricePersistInterval));
            var candles = CandlestickHelper.TradesToCandles(stats, TimeSpan.FromMinutes(_candleWidth));
            CandlestickHelper.CompleteCandles(candles, time);

            if (candles.Count == 0)
            {
                return null;
            }

            if (time - TimeSpan.FromSeconds(_interval) > candles[candles.Count - 1].Start)
            {
                // CheckSignals should be done only once per candle interval - at the very begining
                _log.Info($"Waiting for next candle... Est: {(int)(candles.Last().Start.AddMinutes(_candleWidth) - time).TotalSeconds} seconds");
                return null;
            }

            // !!THIS IS INCORRECT!! Not always it's last
            // Removing last (just opened) candle
            candles.RemoveAt(candles.Count - 1);
            if (candles.Count == 0)
            {
                return null;
            }

            _log.Info("Verifying signals...");

            foreach (var signal in _signals)
            {
                if (signal.Verify(candles))
                    return signal;
            }

            return null;
        }

        protected override bool IsStopLoss(Order order, double price)
        {
            var cutofftime = (_emulation ? _emulationDateTime : DateTime.UtcNow) - TimeSpan.FromMinutes(_stopLossDelay);
            if (order.CreatedAt > cutofftime)
                return false;

            var placedPrice = order.Price / (1 + _takeProfit);
            if (price <= placedPrice - placedPrice * _stopLossPercent)
                return true;
            return false;
        }
    }
}