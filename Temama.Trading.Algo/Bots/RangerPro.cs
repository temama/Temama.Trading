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
        private enum GameState
        {
            None,
            New,
            PlacedBuy,
            PlacedSell
        }

        private double _inRangeTradesPercent = 0.95;
        private double _minutesToAnalise;
        private double _volatilityRate;
        private double _takeProfit;
        private int _pastPossibleTrades = 0;
        private IExchangeAnalitics _analitics;

        private GameState _gameState = GameState.None;
        private double _bottom = 0.0;
        private double _top = 0.0;
        private double _buyPrice = 0.0;
        private double _sellPrice = 0.0;

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
            
            _minutesToAnalise = Convert.ToInt32(config.GetConfigValue("MinutesToAnalyze"), CultureInfo.InvariantCulture);
            
            _volatilityRate = Convert.ToDouble(config.GetConfigValue("VolatilityRate"), CultureInfo.InvariantCulture) * 0.01;
            _takeProfit = Convert.ToDouble(config.GetConfigValue("TakeProfit"), CultureInfo.InvariantCulture) * 0.01;

            if (_takeProfit > _volatilityRate)
                throw new Exception("TakeProfit could not be more than VolatilityRate");

            _inRangeTradesPercent = Convert.ToDouble(config.GetConfigValue("InRangePricePercent"), CultureInfo.InvariantCulture) * 0.01;
            _pastPossibleTrades = Convert.ToInt32(config.GetConfigValue("PastPossibleTrades"));
        }
        
        protected override void TradingIteration(DateTime iterationTime)
        {
            var trades = _analitics.GetRecentTrades(_base, _fund, iterationTime.AddMinutes(-1 * _minutesToAnalise));

            if (_gameState == GameState.None)
                CheckIfNewGame(trades);
            else
                CheckIfGameOver(trades);

            if (_gameState != GameState.None)
            {
                switch (_gameState)
                {
                    case GameState.New:
                        {
                            var allowedFunds = GetLimitedFundsAmount();
                            if (allowedFunds > _minFundToTrade)
                            {
                                var amount = _api.CalculateBuyVolume(_buyPrice, GetAlmolstAll(allowedFunds));
                                PlaceLimitOrder(_base, _fund, "buy", amount, _buyPrice);
                                _gameState = GameState.PlacedBuy;
                            }
                            else
                                _gameState = GameState.None;
                            break;
                        }
                    case GameState.PlacedBuy:
                        {
                            var orders = _api.GetMyOrders(_base, _fund);
                            if (orders.Where(o => o.Side == "buy").Count() == 0)
                            {
                                // Buy order filled
                                var allowedBase = GetLimitedBaseAmount();
                                if (allowedBase > _minBaseToTrade)
                                {
                                    PlaceLimitOrder(_base, _fund, "sell", _api.GetRoundedSellVolume(GetAlmolstAll(allowedBase)), _sellPrice);
                                    _gameState = GameState.PlacedSell;
                                }
                                else
                                    // Not enough amount to resume game
                                    _gameState = GameState.None;
                            }
                            break;
                        }
                    case GameState.PlacedSell:
                        {
                            var orders = _api.GetMyOrders(_base, _fund);
                            if (orders.Where(o => o.Side == "sell").Count() == 0)
                            {
                                // Sell order filled
                                _gameState = GameState.None;
                            }
                            break;
                        }
                    default:
                        // Not sure why we are here
                        _gameState = GameState.None;
                        break;
                }
                


            }
        }

        protected override bool IsStopLoss(Order order, double price)
        {
            if (_gameState != GameState.None)
                return false;

            var cutofftime = (_emulation ? _emulationDateTime : DateTime.UtcNow) - TimeSpan.FromMinutes(_stopLossDelay);
            if (order.CreatedAt > cutofftime)
                return false;

            var placedPrice = order.Price / (1 + _takeProfit / 2.0);
            if (price <= placedPrice - placedPrice * _stopLossPercent)
                return true;

            return false;
        }

        //private void CorrectRange(List<Trade> stats, DateTime iterationTime)
        //{
        //    _log.Info("RangerPro: Range correction...");

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
        //    _log.Info(string.Format("Prices for last {0} hours: min={1}; max={2}; mid={3}; balancedMid={4}",
        //        _hoursToAnalyze, minPrice, maxPrice, midPrice, balancedMidPrice));

        //    _priceToBuy = Math.Round(balancedMidPrice - balancedMidPrice * _percentToBuy, 4);
        //    _priceToSell = Math.Round(balancedMidPrice + balancedMidPrice * _percentToSell, 4);

        //    var msg = $"Range was corrected to: Buy={_priceToBuy}; Sell={_priceToSell}";
        //    _log.Info("!!! " + msg);
        //    NotificationManager.SendInfo(WhoAmI, msg);

        //    var orders = _api.GetMyOrders(_base, _fund);
        //    foreach (var ord in orders)
        //    {
        //        if ((iterationTime - ord.CreatedAt).TotalHours >= _hoursToAnalyze)
        //        {
        //            var closest = double.MaxValue;
        //            foreach (var tick in stats)
        //            {
        //                if (Math.Abs(ord.Price - tick.Price) < closest)
        //                    closest = Math.Abs(ord.Price - tick.Price);
        //            }
        //            _log.Info(string.Format("WARN: for last {0} hours closest price diff with [{1}] order was:{2}",
        //                _hoursToAnalyze, ord, closest));
        //        }
        //    }
        //}
 
        private void CheckIfNewGame(List<Trade> recentTrades)
        {
            if (_gameState != GameState.None || recentTrades.Count == 0)
                return;

            var n = recentTrades.Count;
            var mid = 0.0;
            
            foreach (var t in recentTrades)
            {
                mid += t.Price / n;
            }

            var top = mid + mid * (_volatilityRate / 2);
            var bottom = mid - mid * (_volatilityRate / 2);
            var outsideN = 0;

            foreach (var t in recentTrades)
            {
                if (t.Price > top || t.Price < bottom)
                    outsideN++;
            }

            var inRangePercent = 1 - outsideN / n;
            if (inRangePercent < _inRangeTradesPercent)
                return;

            var topT = mid + mid * (_takeProfit / 2);
            var bottomT = mid - mid * (_takeProfit / 2);

            var c = 0;
            if (_pastPossibleTrades > 0)
            {
                var trades = recentTrades.Select(t => t.Clone()).ToList();
                trades.Sort(Trade.SortByDate);

                var v = -1;
                for (int i = 0; i < n; i++)
                {
                    if ((v == -1 && trades[i].Price <= bottomT) ||
                        (v == 1 && trades[i].Price > topT))
                    {
                        v = -v;
                        c++;
                    }
                }
                c = c / 2;

                if (c < _pastPossibleTrades)
                    return;
            }

            // If we are here, new game could be started
            _top = top;
            _bottom = bottom;
            _sellPrice = topT;
            _buyPrice = bottomT;
            _gameState = GameState.New;
            _log.Important($"New game in range [{_bottom};{_top}] started. Past possible trades {c}");
            _log.Info($"Buy/Sell Price: {_buyPrice} / {_sellPrice}");
        }

        private void CheckIfGameOver(List<Trade> recentTrades)
        {
            if (_gameState == GameState.None || recentTrades.Count == 0)
                return;

            var outsideN = 0;

            foreach (var t in recentTrades)
            {
                if (t.Price > _top || t.Price < _bottom)
                    outsideN++;
            }

            var inRangePercent = 1 - outsideN / recentTrades.Count;
            if (inRangePercent < _inRangeTradesPercent)
            {
                _log.Important("Game over");
                UpdateIterationStats();
                foreach (var order in _openOrders.Where(o => o.Side == "buy"))
                {
                    CancelOrder(order);
                }
                _gameState = GameState.None;
            }
        }
    }
}
