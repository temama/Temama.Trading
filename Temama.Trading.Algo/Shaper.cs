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

namespace Temama.Trading.Algo
{
    public class Shaper : Algorithm
    {
        private class Strategy
        {
            public enum TradeType
            {
                Percent,
                Shape
            }

            public TradeType Type = TradeType.Percent;
            public List<string> Shapes = new List<string>();
            public double Percent = 0;
        }


        private int _interval = 60;
        private IExchangeAnalitics _analitics;
        private int _candleTime = 300;
        private double _zeroDiff = 0.001;
        private int _candlesToAnalize = 0;
        private Strategy _buyStrategy;
        private Strategy _sellStrategy;
        private int _utcOffset = 0;

        public override void Init(IExchangeApi api, XmlDocument config)
        {
            if (!(api is IExchangeAnalitics))
            {
                throw new Exception(string.Format("Shaper can't run on {0} exchange, as it doesn't implement IExchangeAnalitics",
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
            node = config.SelectSingleNode("//TemamaTradingConfig/CandleTime");
            _candleTime = Convert.ToInt32(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/ZeroDiff");
            _zeroDiff = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            node = config.SelectSingleNode("//TemamaTradingConfig/UtcTimeOffset");
            _utcOffset = Convert.ToInt32(node.InnerText);

            _buyStrategy = new Strategy();
            _sellStrategy = new Strategy();

            node = config.SelectSingleNode("//TemamaTradingConfig/BuyStrategy");
            if (node.Attributes["type"].Value.ToLower() == "shape")
            {
                _buyStrategy.Type = Strategy.TradeType.Shape;
                foreach (XmlNode shapeNode in config.SelectNodes("//TemamaTradingConfig/BuyStrategy/Shape"))
                {
                    var shape = shapeNode.InnerText;
                    _buyStrategy.Shapes.Add(shape);
                    if (shape.Length > _candlesToAnalize)
                        _candlesToAnalize = shape.Length;
                }
            }
            else if (node.Attributes["type"].Value.ToLower() == "percent")
            {
                _buyStrategy.Type = Strategy.TradeType.Percent;
                _buyStrategy.Percent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            }

            node = config.SelectSingleNode("//TemamaTradingConfig/SellStrategy");
            if (node.Attributes["type"].Value.ToLower() == "shape")
            {
                _sellStrategy.Type = Strategy.TradeType.Shape;
                foreach (XmlNode shapeNode in config.SelectNodes("//TemamaTradingConfig/SellStrategy/Shape"))
                {
                    var shape = shapeNode.InnerText;
                    _sellStrategy.Shapes.Add(shape);
                    if (shape.Length > _candlesToAnalize)
                        _candlesToAnalize = shape.Length;
                }
            }
            else if (node.Attributes["type"].Value.ToLower() == "percent")
            {
                _sellStrategy.Type = Strategy.TradeType.Percent;
                _sellStrategy.Percent = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture) * 0.01;
            }

            _pair = _base + "/" + _fund;
        }

        public override Task StartTrading()
        {
            if (Trading)
            {
                Logger.Error("Shaper: Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                Logger.Info(string.Format("Shaper: Starting trading pair {0}...", _pair.ToUpper()));
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

        private void DoTradingIteration()
        {
            var shape = GetCurrentShape();
            Logger.Info(shape);
        }

        public override void StopTrading()
        {
            Logger.Info("Shaper: Stopping trading...");
            Trading = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }

        private string GetCurrentShape()
        {
            var res = "";
            var firstTime = DateTime.UtcNow.AddHours(_utcOffset).AddSeconds(-1 * _candleTime * (_candlesToAnalize + 1));
            var stats = _analitics.GetRecentPrices(_base, _fund, firstTime);
            stats.Sort(SortTickAscByDateTime);

            if (stats.Count == 0)
                return string.Empty;

            var nextTime = firstTime.Date;
            while (nextTime < stats[0].Time)
            {
                if (nextTime > firstTime)
                    res += "?";
                nextTime = nextTime.AddSeconds(_candleTime);
            }
            
            var open = stats[0].Last;
            Logger.Info(string.Format("O:{0}\t{1}", stats[0].Last, stats[0].Time));
            var close = 0.0;
            for (int i = 0; i < stats.Count - 1; i++)
            {                
                if (stats[i + 1].Time > nextTime)
                {
                    close = stats[i].Last;
                    Logger.Info(string.Format("C:{0}\t{1}", stats[i].Last, stats[i].Time));
                    var diff = (close - open) / ((open + close) / 2) * 100;
                    if (Math.Abs(diff) <= _zeroDiff)
                        res += "_";
                    else
                        res += diff > 0 ? "/" : "\\";

                    open = stats[i + 1].Last;
                    Logger.Info(string.Format("O:{0}\t{1}", stats[i+1].Last, stats[i+1].Time));
                    nextTime = nextTime.AddSeconds(_candleTime);
                }

                if ((stats[i + 1].Time - stats[i].Time).TotalSeconds > _candleTime)
                {
                    while (nextTime < stats[i + 1].Time)
                    {
                        res += "_";
                        nextTime = nextTime.AddSeconds(_candleTime);
                    }
                }
            }

            // if currently is second half of candle (nextTime - current time <= 0.5 * _candleTime
            // add figure to shape
            if ((nextTime - DateTime.UtcNow.AddHours(_utcOffset)).TotalSeconds <= 0.5 * _candleTime)
            {
                close = stats[stats.Count - 1].Last;
                Logger.Info(string.Format("C:{0}\t{1}", stats[stats.Count - 1].Last, stats[stats.Count - 1].Time));
                var diff = (close - open) / ((open + close) / 2) * 100;
                if (Math.Abs(diff) <= _zeroDiff)
                    res += "_";
                else
                    res += diff > 0 ? "/" : "\\";
            }

            return res;
        }

        private int SortTickAscByDateTime(Tick first, Tick second)
        {
            if (first.Time < second.Time)
                return -1;
            else if (second.Time < first.Time)
                return 1;
            else return 0;
        }
    }
}
