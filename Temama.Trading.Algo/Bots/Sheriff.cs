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
    public class Sheriff : TradingBot
    {
        private double _imbalanceValue;
        
        private double _sellFarPercent;
        private double _sellNearPercent;
        private double _buyNearPercent;
        private double _buyFarPercent;
        private bool _allowAutoBalanceOrders = false; // refer to description of _imbalanceValue;       
        private TimeSpan _gameDuration = TimeSpan.FromHours(24);

        public override string Name()
        {
            return "Sheriff";
        }

        public Sheriff(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        { }

        protected override void InitAlgo(XmlNode config)
        {
            _sellFarPercent = Convert.ToDouble(config.GetConfigValue("SellFarPercent"), CultureInfo.InvariantCulture) * 0.01;
            _sellNearPercent = Convert.ToDouble(config.GetConfigValue("SellNearPercent"), CultureInfo.InvariantCulture) * 0.01;
            _buyNearPercent = Convert.ToDouble(config.GetConfigValue("BuyNearPercent"), CultureInfo.InvariantCulture) * 0.01;
            _buyFarPercent = Convert.ToDouble(config.GetConfigValue("BuyFarPercent"), CultureInfo.InvariantCulture) * 0.01;
            _allowAutoBalanceOrders = Convert.ToBoolean(config.GetConfigValue("AllowAutoBalanceTrades"));
            _imbalanceValue = Convert.ToDouble(config.GetConfigValue("ImbalanceValue"), CultureInfo.InvariantCulture);
            _gameDuration = TimeSpan.FromHours(Convert.ToDouble(config.GetConfigValue("GameDuration"), CultureInfo.InvariantCulture));

            _pair = _base + _fund;
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            // 0. If game over
            var newGame = _openOrders.Count == 0;
            foreach (var order in _openOrders)
            {
                if (dateTime - order.CreatedAt > _gameDuration)
                {
                    newGame = true;
                    break;
                }
            }

            if (newGame)
            {
                _log.Important("Starting new game...");
                if (_openOrders.Count > 0)
                {
                    _log.Info("Open orders will be cancelled");
                    foreach (var order in _openOrders)
                    {
                        _api.CancellOrder(order);
                        NotifyOrderCancel(order);
                    }
                }

                // 1. Check for imbalance. If we have much more of one currency then another -
                //        current algorithm may be not efficient
                if (_openOrders.Count == 0 && _allowAutoBalanceOrders)
                {
                    if (!BalanceFunds())
                    {
                        // If we are here - there is imbalance, but
                        // no orders with acceptable price to fix this
                        // Skipping iteration so far
                        return;
                    }
                    UpdateIterationStats();
                }
            }

            // 2. If we have no buy orders and have many (>2) sell orders, convert far away sell orders to funds. 
            //    And vice versa
            var _buyOrders = _openOrders.Where(o => o.Side == "buy");
            var _sellOrders = _openOrders.Where(o => o.Side == "sell");
            
            // 3. Place buy/sell orders if possible
            PlaceBuyOrders(_buyOrders.Count() == 0);
            PlaceSellOrders(_sellOrders.Count() == 0);
        }

        private void PlaceBuyOrders(bool splitAmount)
        {
            var amount = GetAlmolstAll(GetLimitedFundsAmount());
            var last = _lastPrice;

            if (amount < _minFundToTrade)
            {
                _log.Spam("Not enough funds to place buy orders");
                return;
            }

            if (splitAmount && amount > _minFundToTrade * 2)
            {
                amount = amount / 2;
                var order = _api.PlaceOrder(_base, _fund, "buy", _api.CalculateBuyVolume(last - last * _buyNearPercent, amount),
                    last - last * _buyNearPercent);
                NotifyOrderPlaced(order);
                order = _api.PlaceOrder(_base, _fund, "buy", _api.CalculateBuyVolume(last - last * _buyFarPercent, amount),
                    last - last * _buyFarPercent);
                NotifyOrderPlaced(order);
            }
            else
            {
                var order = _api.PlaceOrder(_base, _fund, "buy", _api.CalculateBuyVolume(last - last * _buyNearPercent, amount),
                    last - last * _buyNearPercent);
                NotifyOrderPlaced(order);
            }
        }

        private void PlaceSellOrders(bool splitAmount)
        {
            var amount = GetAlmolstAll(GetLimitedBaseAmount());
            var last = _lastPrice;

            if (amount < _minBaseToTrade)
            {
                _log.Spam("Not enough base currency to place buy orders");
                return;
            }

            if (splitAmount && amount > _minBaseToTrade * 2)
            {
                amount = amount / 2;
                var order = _api.PlaceOrder(_base, _fund, "sell", _api.GetRoundedSellVolume(amount),
                    last + last * _buyNearPercent);
                NotifyOrderPlaced(order);
                order = _api.PlaceOrder(_base, _fund, "sell", _api.GetRoundedSellVolume(amount),
                    last + last * _buyFarPercent);
                NotifyOrderPlaced(order);
            }
            else
            {
                var order = _api.PlaceOrder(_base, _fund, "sell", _api.GetRoundedSellVolume(amount),
                    last + last * _buyNearPercent);
                NotifyOrderPlaced(order);
            }
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="price"></param>
        /// <returns>False if imbalance, but buy/sell failed for some reason</returns>
        private bool BalanceFunds()
        {
            var funds = _api.GetFunds(_base, _fund);
            var fBase = funds.Values[_base];
            var fFund = funds.Values[_fund];
            if (fFund < _minFundToTrade && fBase < _minBaseToTrade)
            {
                // Nothing to do here...
                return true;
            }

            if ((fBase * _lastPrice) / fFund > _imbalanceValue ||
                fFund / (fBase * _lastPrice) > _imbalanceValue)
            {
                _log.Warning(string.Format("Funds imbalance detected: {0}", funds));
                if (fBase * _lastPrice > fFund)
                {
                    // We have more assets in BTC, need to sell some
                    var sellDiffInUah = (fBase * _lastPrice - fFund) / 2;
                    var sellAmountBtc = sellDiffInUah / _lastPrice;
                    if (!SellByMarketPrice(sellAmountBtc, _lastPrice - _lastPrice * _buyNearPercent))
                    {
                        _log.Error(string.Format("CheckForImbalance: Sell by market price failed. Amount: {0}BTC", sellAmountBtc));
                        return false;
                    }
                }
                else
                {
                    // We have more assets in UAH, need to buy some BTC
                    var buyDiffInUah = (fFund - fBase * _lastPrice) / 2;
                    if (!BuyByMarketPrice(buyDiffInUah, _lastPrice + _lastPrice * _sellNearPercent))
                    {
                        _log.Error(string.Format("CheckForImbalance: Buy by market price failed. Amount: {0}UAH", buyDiffInUah));
                        return false;
                    }
                }
            }
            return true;
        }

        protected override bool IsStopLoss(Order order, double price)
        {
            var cutofftime = (_emulation ? _emulationDateTime : DateTime.UtcNow) - TimeSpan.FromMinutes(_stopLossDelay);
            if (order.CreatedAt > cutofftime)
                return false;

            return ((order.Price - price) / price >= _stopLossPercent);
        }
    }
}
