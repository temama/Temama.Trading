using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class Juggler : TradingBot
    {
        public static int OperationCheckDelay = 2000;

        private enum StepType
        {
            Transfer,
            Buy,
            Sell
        }

        private enum OrderType
        {
            Market,
            Limit
        }

        private enum FeeType
        {
            Absolute,
            Percent
        }

        private abstract class Step
        {
            public abstract StepType Type { get; }
            public int Order { get; set; }
            public double Fee { get; set; }
            public FeeType FeeType { get; set; }
            public ExchangeApi Api { get; set; }
            public Juggler Bot { get; set; }
            public int Interval { get; set; }
            public int Timeout { get; set; }

            public double LastPrice { get; protected set; }
            public double LastVolume { get; protected set; }

            public static int SortByOrder(Step first, Step second)
            {
                if (first.Order < second.Order)
                    return -1;
                else if (second.Order < first.Order)
                    return 1;
                else return 0;
            }

            public abstract bool IsReadyToImplement(double inAmount);

            public abstract StepImplementResult Implement(double inAmount);

            public abstract double Test(double inAmount);

            public abstract double DryRunLast(double inAmount);

            public virtual double SubtractFee(double input)
            {
                var res = input;
                if (FeeType == FeeType.Percent)
                    res = res - res * Fee;
                else
                    res = res - Fee;
                return res;
            }

            public static Step Parse(XmlNode node, Juggler bot)
            {
                Step step;
                var type = node.Attributes["type"].Value;
                switch (type.ToLower())
                {
                    case "buy":
                        step = new StepBuy(node);
                        break;
                    case "sell":
                        step = new StepSell(node);
                        break;
                    case "trans":
                        step = new StepTransfer(node);
                        break;
                    default:
                        throw new Exception($"Unknown step name={type}");
                }

                step.Bot = bot;

                var order = node.Attributes["order"].Value;
                step.Order = Convert.ToInt32(order);

                var fee = node.Attributes["fee"].Value;
                if (fee.Contains("%"))
                {
                    step.FeeType = FeeType.Percent;
                    step.Fee = Convert.ToDouble(fee.Replace("%", "").Trim(), CultureInfo.InvariantCulture) * 0.01;
                }
                else
                {
                    step.FeeType = FeeType.Absolute;
                    step.Fee = Convert.ToDouble(fee, CultureInfo.InvariantCulture);
                }

                if (node.Attributes["interval"] != null)
                {
                    step.Interval = Convert.ToInt32(node.Attributes["interval"].Value);
                }
                else
                    step.Interval = 0;

                if (node.Attributes["timeout"] != null)
                {
                    step.Timeout = Convert.ToInt32(node.Attributes["timeout"].Value);
                }
                else
                    step.Timeout = 0;

                var api = node.Attributes["api"].Value;
                step.Api = bot.Apis[api];

                return step;
            }
        }

        private class StepBuy : Step
        {
            public string Base { get; set; }
            public string Fund { get; set; }
            public OrderType OrderType { get; set; } = OrderType.Market;

            public override StepType Type => StepType.Buy;

            public StepBuy(XmlNode node)
            {
                Base = node.Attributes["base"].Value;
                Fund = node.Attributes["fund"].Value;
                if (node.Attributes["order"] != null)
                {
                    switch (node.Attributes["order"].Value.ToString().ToLower())
                    {
                        case "limit":
                            OrderType = OrderType.Limit;
                            break;
                        case "market":
                            OrderType = OrderType.Market;
                            break;
                        default:
                            break;
                    }
                }
            }

            public override bool IsReadyToImplement(double inAmount)
            {
                var f =  Api.GetFunds(Base, Fund);
                return f.Values[Fund] >= inAmount;
            }

            public override StepImplementResult Implement(double inAmount)
            {
                if (OrderType == OrderType.Market)
                {
                    var f = Api.GetFunds(Base, Fund);
                    if (Bot.BuyByMarketPrice(inAmount))
                    {
                        var fa = Api.GetFunds(Base, Fund);
                        return new StepImplementResult()
                        {
                            Success = true,
                            OutputAmount = fa.Values[Base] - f.Values[Base]
                        };
                    }
                    else
                        return new StepImplementResult() { Success = false, OutputAmount = 0 };
                }
                else if (OrderType == OrderType.Limit)
                {
                    var startTime = DateTime.UtcNow;
                    var volume = Api.CalculateBuyVolume(LastPrice, LastVolume);
                    var order = Api.PlaceOrder(Base, Fund, "buy", volume, LastPrice);
                    var waiting = true;
                    while (waiting)
                    {
                        var orders = Api.GetMyOrders(Base, Fund);
                        if (!orders.Any(o => o.Id == order.Id))
                        {
                            waiting = false;
                            break;
                        }

                        if (Timeout != 0 && (DateTime.UtcNow - startTime).TotalSeconds > Timeout)
                        {
                            throw new Exception($"Timeout on step {ToString()}");
                        }

                        Thread.Sleep(OperationCheckDelay);
                    }
                    return new StepImplementResult { Success = true, OutputAmount = SubtractFee(volume) };
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            public override double Test(double inAmount)
            {
                var ob = Api.GetOrderBook(Base, Fund);
                LastPrice = ob.Asks[0].Price;
                LastVolume = ob.Asks[0].Volume;
                var marketBestPrice = ob.Asks[0].Price;
                var res = inAmount / marketBestPrice;
                return SubtractFee(res);
            }

            public override double DryRunLast(double inAmount)
            {
                return SubtractFee(Math.Min(inAmount, LastVolume) / LastPrice);
            }

            public override string ToString()
            {
                return $"{Api.Name()} {Type} {Base} {Fund}";
            }
        }

        private class StepSell : Step
        {
            public string Base { get; set; }
            public string Fund { get; set; }
            public OrderType OrderType { get; set; } = OrderType.Market;

            public override StepType Type => StepType.Sell;

            public StepSell(XmlNode node)
            {
                Base = node.Attributes["base"].Value;
                Fund = node.Attributes["fund"].Value;
                if (node.Attributes["order"] != null)
                {
                    switch (node.Attributes["order"].Value.ToString().ToLower())
                    {
                        case "limit":
                            OrderType = OrderType.Limit;
                            break;
                        case "market":
                            OrderType = OrderType.Market;
                            break;
                        default:
                            break;
                    }
                }
            }

            public override bool IsReadyToImplement(double inAmount)
            {
                var f = Api.GetFunds(Base, Fund);
                return f.Values[Base] >= inAmount;
            }

            public override StepImplementResult Implement(double inAmount)
            {
                var f = Api.GetFunds(Base, Fund);
                if (Bot.SellByMarketPrice(inAmount))
                {
                    var fa = Api.GetFunds(Base, Fund);
                    return new StepImplementResult()
                    {
                        Success = true,
                        OutputAmount = fa.Values[Fund] - f.Values[Fund]
                    };
                }
                else
                    return new StepImplementResult() { Success = false, OutputAmount = 0 };
            }

            public override double Test(double inAmount)
            {
                var ob = Api.GetOrderBook(Base, Fund);
                LastPrice = ob.Bids[0].Price;
                LastVolume = ob.Bids[0].Volume;
                var marketBestPrice = ob.Bids[0].Price;
                var res = inAmount * marketBestPrice;
                return SubtractFee(res);
            }

            public override double DryRunLast(double inAmount)
            {
                return SubtractFee(Math.Min(inAmount, LastVolume) * LastPrice);
            }

            public override string ToString()
            {
                return $"{Api.Name()} {Type} {Base} {Fund}";
            }
        }

        private class StepTransfer : Step
        {
            public string Base { get; set; }
            public string Wallet { get; set; }

            public override StepType Type => StepType.Transfer;

            public StepTransfer(XmlNode node)
            {
                Base = node.Attributes["base"].Value;
                Wallet = node.Attributes["wallet"].Value;
                LastPrice = double.MaxValue;
                LastVolume = double.MaxValue;
            }

            public override bool IsReadyToImplement(double inAmount)
            {
                var f = Api.GetFunds(Base, Base);
                return f.Values[Base] >= inAmount;
            }

            public override StepImplementResult Implement(double inAmount)
            {
                Api.Withdraw(Base, Wallet);
                var rest = inAmount;
                if (FeeType == FeeType.Percent)
                    rest = rest - rest * Fee;
                else
                    rest = rest - Fee;
                return new StepImplementResult() { Success = true, OutputAmount = rest };
            }

            public override double Test(double inAmount)
            {
                var rest = inAmount;
                if (FeeType == FeeType.Percent)
                    rest = rest - rest * Fee;
                else
                    rest = rest - Fee;
                return rest;
            }

            public override double DryRunLast(double inAmount)
            {
                return SubtractFee(inAmount);
            }

            public override string ToString()
            {
                return $"{Api.Name()} {Type} {Base}";
            }
        }

        private class ScenarioResult
        {
            public double Profit { get; set; }
            public double ExpectedRes { get; set; }
        }

        private class StepImplementResult
        {
            public bool Success { get; set; }
            public double OutputAmount { get; set; }
        }

        public Dictionary<string, ExchangeApi> Apis { get { return _apis; } }
        public bool MonitorMode { get { return _monitorMode; } }

        private double _profitToPlay = 0.05;
        private Dictionary<string, ExchangeApi> _apis = new Dictionary<string, ExchangeApi>();
        private List<Step> _steps = new List<Step>();
        private bool _inGame = false;
        private int _currentStep = 0;
        private bool _monitorMode = false;
        private int _globalInterval = 30;
        private string _veryBase;
        private double _beforeRunFiat = 0;
        private double _currentAmountToOperate = 0;
        private double _operatingAmount = 0.0;
        private bool _useRecomendedFunds = false;

        public Juggler(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            _apis.Clear();
            _steps.Clear();

            // Load APIs
            var apiNodes = config.SelectNodes("Api");
            foreach (XmlNode node in apiNodes)
            {
                var api = Globals.CreateApiByName(node.Attributes["exchange"].Value, node, _log);
                var name = node.Attributes["name"].Value;
                _apis[name] = api;
            }

            // Load params
            _monitorMode = Convert.ToBoolean(config.GetConfigValue("MonitorMode", true, "false"));
            _useRecomendedFunds = Convert.ToBoolean(config.GetConfigValue("UseRecomendedFunds", true, "false"));
            _profitToPlay = Convert.ToDouble(config.GetConfigValue("ProfitToPlay"), CultureInfo.InvariantCulture) * 0.01;
            _operatingAmount = Convert.ToDouble(config.GetConfigValue("OperatingAmount"), CultureInfo.InvariantCulture);
            _veryBase = config.GetConfigValue("VeryBase").ToUpper();

            // Load Steps
            var stepNodes = config.SelectNodes("Steps/Step");
            foreach (XmlNode node in stepNodes)
            {
                _steps.Add(Step.Parse(node, this));
            }

            _steps.Sort(Step.SortByOrder);

            if (_apis.Count == 0)
                throw new Exception("Config should containt at least 1 API");

            if (_steps.Count == 0)
                throw new Exception("Config should containt at least 1 step");

            _api = _apis.First().Value;
            _globalInterval = _interval;
            _pair = _veryBase + "/" + _veryBase;
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_inGame)
            {
                var res = VerifyScenario();
                var msg = $"{DisplayName}: Scenario profit={(res.Profit * 100).ToString("0.##")}%; ExpectedRes={res.ExpectedRes.ToString("0.##")}/{_operatingAmount}{_veryBase}";
                _log.Info(msg);
                if (res.Profit >= _profitToPlay)
                {
                    TryToStartGame(res);
                }
            }

            if (_inGame)
            {
                try
                {
                    GameIteration();
                }
                catch (Exception ex)
                {
                    _inGame = false;
                    _currentStep = 0;
                    SetupStep(_steps[0]);

                    _log.Critical("Game step failed with exception.. stopping game");

                    throw ex;
                }
            }
        }

        private void GameIteration()
        {
            var step = _steps[_currentStep];
            _log.Info($"{DisplayName} Step: {step}");
            if (step.IsReadyToImplement(_currentAmountToOperate))
            {
                _log.Info($"{DisplayName} Step is Ready");
                var res = step.Implement(_currentAmountToOperate);
                if (res.Success)
                {
                    _log.Important($"{DisplayName} Step DONE: {step}");
                    _currentAmountToOperate = res.OutputAmount;
                    _currentStep++;

                    if (_currentStep >= _steps.Count)
                    {
                        _inGame = false;
                        _currentStep = 0;
                        SetupStep(_steps[0]);
                        var afterRun = _steps[0].Api.GetFunds(_veryBase, _veryBase).Values[_veryBase];
                        _log.Important($"Scenario completted. Profit={afterRun - _beforeRunFiat}{_veryBase}");
                    }
                    else
                    {
                        step = _steps[_currentStep];
                        SetupStep(step);
                    }
                }
                else
                {
                    _log.Error($"Step implementation failed: {step}");
                }
            }
        }

        private void TryToStartGame(ScenarioResult res)
        {
            var msg = $"{DisplayName}: Scenario profit={(res.Profit * 100).ToString("0.##")}%; ExpectedRes={res.ExpectedRes.ToString("0.##")}/{_operatingAmount}{_veryBase}";
            NotificationManager.SendImportant(WhoAmI, msg);
            var availableFunds = GetAvailableFundsToStart();
            var amountToStart = _operatingAmount;

            if (_useRecomendedFunds)
            {
                amountToStart = CalculateRecomendedAmountToStart(res);
            }

            if (availableFunds >= amountToStart)
            {
                _currentAmountToOperate = _operatingAmount;
                if (!_monitorMode)
                {
                    _log.Important($"Starting scenario... Expected profit={(res.Profit * 100).ToString("0.##")}%");
                    _inGame = true;
                    _currentStep = 0;
                    SetupStep(_steps[0]);
                    _beforeRunFiat = _steps[0].Api.GetFunds(_veryBase, _veryBase).Values[_veryBase];
                }
                else
                {
                    _log.Important($"Monitor mode... Found profit={(res.Profit * 100).ToString("0.##")}; With ExpectedRes={res.ExpectedRes.ToString("0.##")}");
                }
            }
            else
            {
                _log.Warning($"Not enough funds to start game");
            }
        }

        private double CalculateRecomendedAmountToStart(ScenarioResult res)
        {
            var op = _operatingAmount;
            for (int i = 0; i < _steps.Count; i++)
            {
                op = _steps[i].DryRunLast(op);
            }

            return op - op * res.Profit;
        }

        private void SetupStep(Step step)
        {
            _api = step.Api;
            if (step.Interval > 0)
                _interval = step.Interval;
            else
                _interval = _globalInterval;

            if (step is StepBuy)
            {
                var sb = step as StepBuy;
                _base = sb.Base;
                _fund = sb.Fund;
            }
            else if (step is StepSell)
            {
                var ss = step as StepSell;
                _base = ss.Base;
                _fund = ss.Fund;
            }
            else if (step is StepTransfer)
            {
                var st = step as StepTransfer;
                _base = st.Base;
            }
        }

        private ScenarioResult VerifyScenario()
        {
            var res = _operatingAmount;
            for (int i = 0; i < _steps.Count; i++)
            {
                res = _steps[i].Test(res);
                if (res <= 0)
                    break;
            }

            return new ScenarioResult() { Profit = (res - _operatingAmount) / _operatingAmount, ExpectedRes = res };
        }

        private double GetAvailableFundsToStart()
        {
            var f = _api.GetFunds(_veryBase, _veryBase);
            if (f.Values[_veryBase] < _minBaseToTrade)
                return 0;

            return Math.Min(f.Values[_veryBase], _maxFundsToOperate);
        }

        protected override void UpdateIterationStats()
        {
            // Do nothing
        }

        public override double GetFiatBalance()
        {
            if (_inGame)
            {
                _log.Info("Scenario in progress, skipping GetFiatBalance");
                return 0.0;
            }

            var f = _api.GetFunds(_veryBase, _veryBase);
            return f.Values[_veryBase];
        }

        protected override void PrintSummary()
        {
            // Do nothing
        }
    }
}
