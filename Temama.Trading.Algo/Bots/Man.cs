using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Common;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class Man : TradingBot
    {
        private int _maLong = 50;
        private int _maShort = 5;
        private double _candleWidth = 15;
        private IExchangeAnalitics _analitics;

        public Man(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            if (!(_api is IExchangeAnalitics))
            {
                throw new Exception($"RangerPro can't run on {_api.Name()} exchange, as it doesn't implement IExchangeAnalitics");
            }
            else
                _analitics = _api as IExchangeAnalitics;

            _maLong = Convert.ToInt32(config.GetConfigValue("MAnLongTerm"));
            _maShort = Convert.ToInt32(config.GetConfigValue("MAnShortTerm"));
            _candleWidth = Convert.ToDouble(config.GetConfigValue("CandleWith"), CultureInfo.InvariantCulture);

            if (_pricePersistInterval == 0)
            {
                _pricePersistInterval = (int)_candleWidth * (_maLong + 2);
            }
            _analitics.SetHistoricalTradesPersistInterval(_base, _fund, TimeSpan.FromMinutes(_pricePersistInterval));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_analitics.HasHistoricalDataStartingFrom(_base, _fund,
                dateTime.AddMinutes(-1 * _pricePersistInterval), true))
            {
                _log.Info("Not enough historical data to perform iteration");
                return;
            }

            var stats = _analitics.GetRecentTrades(_base, _fund, dateTime.AddMinutes(-1 * _pricePersistInterval));
            var candles = CandlestickHelper.TradesToCandles(stats, TimeSpan.FromMinutes(_candleWidth));
            CandlestickHelper.CompleteCandles(candles, dateTime);

            // Removing last (just opened) candle
            candles.RemoveAt(candles.Count - 1);
            if (candles.Count == 0)
            {
                // WTF?
                return;
            }

            var prevLastPrice = GetPriceAt(dateTime.AddSeconds(-1 * _interval), stats);
            var lastPrice = stats.Last().Price;
            var maL = SimpleMovingAvarage(candles, _maLong);
            var maS = SimpleMovingAvarage(candles, _maShort);

            candles.Remove(candles.Last());

            var maSPrev = SimpleMovingAvarage(candles, _maShort);

            var shouldBuy = (prevLastPrice < maL) && (lastPrice > maL) && (maS > maSPrev);
            var shouldSell = maS < maSPrev;

            if (shouldBuy)
            {
                var funds = GetAlmolstAll(GetLimitedFundsAmount());
                if (funds > _minFundToTrade)
                {
                    BuyByMarketPrice(funds);
                }
            }

            if (shouldSell)
            {
                var bases = GetAlmolstAll(GetLimitedBaseAmount());
                if (bases > _minBaseToTrade)
                {
                    SellByMarketPrice(bases);
                }
            }
        }

        private double SimpleMovingAvarage(List<Candlestick> candles, int n)
        {
            var sum = 0.0;
            for (int i = candles.Count - 1; i >= candles.Count - n; i--)
            {
                sum += candles[i].Close;
            }
            return sum / n;
        }

        private double GetPriceAt(DateTime dateTime, List<Trade> stats)
        {
            for (int i = stats.Count - 1; i > 0; i--)
            {
                if (stats[i].CreatedAt <= dateTime)
                    return stats[i].Price;
            }
            return stats[0].Price;
        }
    }
}
