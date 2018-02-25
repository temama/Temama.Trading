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
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Core.Algo
{
    public abstract class TradingBot : IAlgo
    {
        public bool Running { get; protected set; }
        public bool AutoStart { get; private set; }
        public string DisplayName { get; private set; }

        protected ExchangeApi _api;
        protected int _criticalsCount = 0;
        protected int _maxCriticalsCount = 100;
        protected int _interval = 60;
        protected string _base;
        protected string _fund;
        protected string _pair = "Unknown pair";
        protected bool _emulation = false;
        protected DateTime _emulationDateTime;
        protected Logger.Logger _log;

        protected TimeSpan _marketOrderFillingTimeout = TimeSpan.FromMinutes(1);

        protected Task _tradingTask;
        protected DateTime _lastFiatBalanceCheckTime = DateTime.MinValue;
        protected TimeSpan _FiatBalanceCheckInterval = TimeSpan.FromMinutes(10);
        protected double _lastFiatBalance = 0;

        protected double _minBaseToTrade;
        protected double _minFundToTrade;
        protected double _maxFundsToOperate = double.MaxValue;

        protected bool _stopLossEnabled = false;
        protected double _stopLossPercent = 0.0;
        protected double _stopLossDelay = 0.0;

        // Iteration stats
        protected bool _iterationStatsUpdated = false;
        protected double _lastPrice = 0;
        protected Funds _funds;
        protected List<Order> _openOrders;

        // Overall stats
        protected int _orderPlaced = 0;
        protected int _orderCancelled = 0;
        protected int _marketTradesDone = 0;

        public string WhoAmI
        {
            get
            {
                return $"{Name()} on {_api.Name()} [{_pair}]";
            }
        }

        public virtual string Name()
        {
            return "TradingBot";
        }

        public TradingBot(XmlNode config, ILogHandler logHandler)
        {
            AutoStart = false;
            var name = Name();
            var nameAttr = config.Attributes["displayName"];
            if (nameAttr != null)
                name = nameAttr.Value;
            DisplayName = name;

            var autostartAttr = config.Attributes["autostart"];
            if (autostartAttr != null)
                AutoStart = Convert.ToBoolean(autostartAttr.Value);

            _log = new Logger.Logger(name, logHandler);

            var apiNode = config.SelectSingleNode("Api");
            if (apiNode == null)
                throw new ArgumentException("<Api> node not found at config");

            _api = Globals.CreateApiByName(apiNode.Attributes["exchange"].Value, apiNode, _log);

            _interval = Convert.ToInt32(config.GetConfigValue("ExecuteInterval"));
            _base = config.GetConfigValue("BaseCurrency", true).ToUpper();
            _fund = config.GetConfigValue("FundCurrency", true).ToUpper();
            _minBaseToTrade = Convert.ToDouble(config.GetConfigValue("MinBaseToTrade"), CultureInfo.InvariantCulture);
            _minFundToTrade = Convert.ToDouble(config.GetConfigValue("MinFundToTrade"), CultureInfo.InvariantCulture);
            _maxFundsToOperate = Convert.ToDouble(config.GetConfigValue("MaxFundToOperate", true, "0"), CultureInfo.InvariantCulture);
            if (_maxFundsToOperate == 0)
                _maxFundsToOperate = double.MaxValue;
            _maxCriticalsCount = Convert.ToInt32(config.GetConfigValue("MaxCriticalsCount", true, "100"));
            _FiatBalanceCheckInterval = TimeSpan.FromMinutes(Convert.ToInt32(config.GetConfigValue("FiatCheckInterval", true, "10")));

            _stopLossEnabled = Convert.ToBoolean(config.GetConfigValue("StopLossEnabled", true, "false"));
            _stopLossPercent = Convert.ToDouble(config.GetConfigValue("StopLossPercent", true, "0"), CultureInfo.InvariantCulture) * 0.01;
            _stopLossDelay = Convert.ToDouble(config.GetConfigValue("StopLossDelay", true, "0"), CultureInfo.InvariantCulture);

            _pair = _base + "/" + _fund;

            InitAlgo(config);
        }

        public virtual Task Start()
        {
            if (Running)
            {
                _log.Error("Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                _log.Info(string.Format("Starting trading pair {0}...", _pair.ToUpper()));
                Running = true;
                while (Running)
                {
                    DoTradingIteration(DateTime.UtcNow);
                    Thread.Sleep(_interval * 1000);
                }
            });
            _tradingTask = task;
            return task;
        }

        public virtual void Stop()
        {
            _log.Info("Stopping trading...");
            Running = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }

        public virtual void Emulate(DateTime start, DateTime end)
        {
            var emu = _api as IExchangeEmulator;
            _emulation = true;
            _emulationDateTime = start;
            emu.SetIterationTime(_emulationDateTime);
            Running = true;
            _log.Important($"Fiat balance: {GetFiatBalance()}");
            while (_emulationDateTime <= end && Running)
            {
                _log.Spam("Emulation: Iter Time: " + _emulationDateTime);
                emu.SetIterationTime(_emulationDateTime);
                if (_emulationDateTime - _lastFiatBalanceCheckTime > _FiatBalanceCheckInterval)
                {
                    var balance = GetFiatBalance();
                    _log.Important($"Emulation time: {_emulationDateTime}; Price={_lastPrice}");
                    _log.Important($"Fiat balance: {balance}");
                    _lastFiatBalanceCheckTime = _emulationDateTime;
                }
                DoTradingIteration(_emulationDateTime);
                _emulationDateTime = _emulationDateTime.AddSeconds(_interval);
            }
            _log.Important($"Fiat balance: {GetFiatBalance()}");
            _log.Important($"Order Placed: {_orderPlaced}");
            _log.Important($"Order cancelled: {_orderCancelled}");
            _log.Important($"Market trades done: {_marketTradesDone}");
        }

        public virtual double GetFiatBalance()
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            var funds = _api.GetFunds(_base, _fund);
            var sum = funds.Values[_fund] + funds.Values[_base] * _lastPrice;
            foreach (var order in _openOrders)
            {
                if (order.Side == "sell")
                    sum += order.Volume * _lastPrice;
                else
                    sum += order.Volume * order.Price;
            }

            return sum;
        }

        public virtual double GetFundsInOrders()
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            var sum = 0.0;
            foreach (var order in _openOrders)
            {
                if (order.Side == "sell")
                    sum += order.Volume * _lastPrice;
                else
                    sum += order.Volume * order.Price;
            }

            return sum;
        }

        protected abstract void InitAlgo(XmlNode config);

        protected abstract void TradingIteration(DateTime dateTime);
        
        protected virtual void DoTradingIteration(DateTime dateTime)
        {
            try
            {
                PrintSummary();

                if (_stopLossEnabled)
                {
                    foreach (var order in _openOrders.Where(o=>o.Side == "sell"))
                    {
                        if (IsStopLoss(order, _lastPrice))
                        {
                            _log.Warning("Cancelling order by stop loss:");
                            _api.CancellOrder(order);
                            NotifyOrderCancel(order);
                            SellByMarketPrice(order.Volume);
                        }
                    }
                }

                TradingIteration(dateTime);

                _iterationStatsUpdated = false;
            }
            catch (Exception ex)
            {
                _log.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        protected virtual void UpdateIterationStats()
        {
            _iterationStatsUpdated = false;
            _lastPrice = _api.GetLastPrice(_base, _fund);
            _funds = _api.GetFunds(_base, _fund);
            _openOrders = _api.GetMyOrders(_base, _fund);
            _iterationStatsUpdated = true;
        }

        protected virtual bool SellByMarketPrice(double baseAmount, double minAcceptablePrice = 0)
        {
            var orderBook = _api.GetOrderBook(_base, _fund);
            var foundPrice = orderBook.FindPriceForSell(baseAmount);
            if (foundPrice < minAcceptablePrice)
            {
                _log.Warning("SellByMarketPrice: OrderBook doesn't have orders to Sell by acceptable price." +
                    $" minAcceptablePrice={minAcceptablePrice}; foundPrice={foundPrice}");
                return false;
            }

            var sellOrder = _api.PlaceOrder(_base, _fund, "sell", _api.GetRoundedSellVolume(baseAmount), foundPrice);
            var placedTime = _emulation ? _emulationDateTime : DateTime.Now;
            var currentTime = placedTime;

            do
            {
                if (_emulation)
                    _emulationDateTime.AddMilliseconds(2000);
                else
                    Thread.Sleep(2000);

                var myOrders = _api.GetMyOrders(_base, _fund);
                if (!myOrders.Any(o => o.Id == sellOrder.Id)) // If placed order is not at Active Orders - it is filled
                {
                    NotifyMarketTradeDone(sellOrder);
                    return true;
                }

                currentTime = _emulation ? _emulationDateTime : DateTime.Now;
            } while (currentTime - placedTime < _marketOrderFillingTimeout);

            _log.Warning($"Failed to Sell by market price before timeout. Canceling order {sellOrder}");
            _api.CancellOrder(sellOrder);
            return false;
        }

        protected virtual bool BuyByMarketPrice(double fundAmount, double maxAcceptablePrice = double.MaxValue)
        {
            var orderBook = _api.GetOrderBook(_base, _fund);
            var foundPrice = orderBook.FindPriceForBuy(fundAmount);
            if (foundPrice > maxAcceptablePrice)
            {
                _log.Warning($"BuyByMarketPrice: OrderBook doesn't have orders to Buy by acceptable price. " +
                    $"maxAcceptablePrice={maxAcceptablePrice}; foundPrice={foundPrice}");
                return false;
            }

            var buyOrder = _api.PlaceOrder(_base, _fund, "buy", _api.CalculateBuyVolume(foundPrice, fundAmount), foundPrice);
            var placedTime = _emulation ? _emulationDateTime : DateTime.Now;
            var currentTime = placedTime;

            do
            {
                if (_emulation)
                    _emulationDateTime.AddMilliseconds(2000);
                else
                    Thread.Sleep(2000);

                var myOrders = _api.GetMyOrders(_base, _fund);
                if (!myOrders.Any(o => o.Id == buyOrder.Id))
                {
                    NotifyMarketTradeDone(buyOrder);
                    return true;
                }

                currentTime = _emulation ? _emulationDateTime : DateTime.Now;
            } while (currentTime - placedTime < _marketOrderFillingTimeout);

            _log.Warning($"Failed to Buy by market price before timeout. Canceling order {buyOrder}");
            _api.CancellOrder(buyOrder);
            return false;
        }

        /// <summary>
        /// Not to over exceed available amout during orders placement (after rounding at API level)
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        protected virtual double GetAlmolstAll(double amount)
        {
            return amount - amount * 0.01;
        }

        /// <summary>
        /// Returns min value from limited available base curr & expectedBaseAmount
        /// </summary>
        /// <param name="expectedBaseAmount"></param>
        /// <returns></returns>
        protected virtual double GetLimitedBaseAmount(double expectedBaseAmount = double.MaxValue)
        {
            _iterationStatsUpdated = false;
            var fundsInOrders = GetFundsInOrders();
            var allowed = _maxFundsToOperate - fundsInOrders;
            if (allowed <= 0)
                return 0;

            var existing = _funds.Values[_base];

            return Math.Min(existing, Math.Min(allowed / _lastPrice, expectedBaseAmount));
        }

        /// <summary>
        /// Returns min value from limited available fund curr & expectedFundsAmount
        /// </summary>
        /// <param name="expectedFundsAmount"></param>
        /// <returns></returns>
        protected virtual double GetLimitedFundsAmount(double expectedFundsAmount = double.MaxValue)
        {
            _iterationStatsUpdated = false;
            var fundsInOrders = GetFundsInOrders();
            var allowed = _maxFundsToOperate - fundsInOrders;
            if (allowed <= 0)
                return 0;

            var existing = _funds.Values[_fund];

            return Math.Min(existing, Math.Min(allowed, expectedFundsAmount));
        }

        protected virtual void PrintSummary()
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            _log.Info($"Last price: {_lastPrice}");
            var sbOrders = new StringBuilder();
            foreach (var order in _openOrders)
            {
                _log.Spam($"Active order: {order}");
                sbOrders.Append($"{(order.Side == "sell" ? "s" : "b")}:{order.Price}({order.Volume}); ");
            }
            _log.Info($"{_openOrders.Count} active orders: {sbOrders}");

            var iterationTime = _emulation ? _emulationDateTime : DateTime.Now;
            if (iterationTime - _lastFiatBalanceCheckTime > _FiatBalanceCheckInterval)
            {
                var sum = GetFiatBalance();
                _lastFiatBalance = sum;

                var profit = "?";
                if (_lastFiatBalance != 0)
                {
                    profit = Math.Round(sum - _lastFiatBalance, 5).ToString();
                    if (!profit.StartsWith("-"))
                        profit = "+" + profit;
                }

                _log.Info($"Fiat amount: {sum} [{profit}] -----------------");
                _lastFiatBalanceCheckTime = _emulation ? _emulationDateTime : DateTime.Now;
            }
        }

        protected virtual void NotifyOrderPlaced(Order order)
        {
            _log.Important($"{WhoAmI} placed order: {order}");
            NotificationManager.SendImportant(WhoAmI, $"Order placed: {order}");
            _orderPlaced++;
        }

        protected virtual void NotifyOrderCancel(Order order)
        {
            _log.Warning($"{WhoAmI} cancelled order: {order}");
            NotificationManager.SendWarning(WhoAmI, $"Order cancelled: {order}");
            _orderCancelled++;
        }

        protected virtual void NotifyMarketTradeDone(Trade trade)
        {
            _log.Important($"{WhoAmI} Market trade done: {trade}");
            NotificationManager.SendImportant(WhoAmI, $"Market trade done: {trade}");
            _marketTradesDone++;
        }

        protected virtual void NotifyMarketTradeDone(Order order)
        {
            _log.Important($"{WhoAmI} Market trade done: {order}");
            NotificationManager.SendImportant(WhoAmI, $"Market trade done: {order}");
            _marketTradesDone++;
        }
        
        /// <summary>
        /// Returns "true" if order should be cancelled by stop loss
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        protected virtual bool IsStopLoss(Order order, double price)
        {
            _log.Warning("Stop loss is not available for " + Name());
            return false;
        }

        /// <summary>
        /// Kind of protection
        /// </summary>
        protected void AddCritical()
        {
            _criticalsCount++;
            if (_criticalsCount >= _maxCriticalsCount)
            {
                _log.Critical("Criticals count exceeded maximum. Something goes wrong. Will stop trading");
                NotificationManager.SendError(WhoAmI, "Exceeded max criticals. Stopping...");
                Stop();
            }
        }

        protected DateTime GetTime()
        {
            return _emulation ? _emulationDateTime : DateTime.UtcNow;
        }

        private TradingBot()
        { }
    }
}
