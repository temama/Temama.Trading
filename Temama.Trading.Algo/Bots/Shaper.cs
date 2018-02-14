using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Common;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo.Bots
{
    public class Shaper : TradingBot
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

        
        private IExchangeAnalitics _analitics;
        private int _candleTime = 300;
        private double _zeroDiff = 0.001;
        private int _candlesToAnalize = 0;
        private Strategy _buyStrategy;
        private Strategy _sellStrategy;
        private int _utcOffset = 0;

        public override string Name()
        {
            return "Shaper";
        }

        public Shaper(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        { }

        protected override void InitAlgo(XmlNode config)
        {
            if (!(_api is IExchangeAnalitics))
            {
                throw new Exception(string.Format("Shaper can't run on {0} exchange, as it doesn't implement IExchangeAnalitics",
                    _api.Name()));
            }
            else
                _analitics = _api as IExchangeAnalitics;

            _candleTime = Convert.ToInt32(config.GetConfigValue("CandleTime"));
            _zeroDiff = Convert.ToDouble(config.GetConfigValue("ZeroDiff"), CultureInfo.InvariantCulture) * 0.01;
            _utcOffset = Convert.ToInt32(config.GetConfigValue("UtcTimeOffset"));

            _buyStrategy = new Strategy();
            _sellStrategy = new Strategy();

            var node = config.SelectSingleNode("BuyStrategy");
            if (node.Attributes["type"].Value.ToLower() == "shape")
            {
                _buyStrategy.Type = Strategy.TradeType.Shape;
                foreach (XmlNode shapeNode in config.SelectNodes("BuyStrategy/Shape"))
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

            node = config.SelectSingleNode("SellStrategy");
            if (node.Attributes["type"].Value.ToLower() == "shape")
            {
                _sellStrategy.Type = Strategy.TradeType.Shape;
                foreach (XmlNode shapeNode in config.SelectNodes("SellStrategy/Shape"))
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
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            var shape = GetCurrentShape();
            _log.Info(shape);
        }
        
        private string GetCurrentShape()
        {
            var res = "";
            var firstTime = DateTime.UtcNow.AddHours(_utcOffset).AddSeconds(-1 * _candleTime * (_candlesToAnalize + 1));
            var stats = _analitics.GetRecentTrades(_base, _fund, firstTime);
            var candles = CandlestickHelper.TradesToCandles(stats, TimeSpan.FromSeconds(_candleTime));
            foreach (var candle in candles)
            {
                _log.Info(candle.ToString());
                var diff = candle.Body;
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
