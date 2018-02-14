using System;
using System.Collections.Generic;
using System.Xml;

namespace Temama.Trading.Core.Common
{
    public class Signal
    {
        public class SignalCandle
        {
            public enum SignalCandleType
            {
                Any,
                Red,
                Green
            }

            public SignalCandleType Type { get; set; }
            public string UpperShadowExpr { get; set; }
            public string BodyExpr { get; set; }
            public string LowerShadowExpr { get; set; }
            public string VolumeExpr { get; set; }

            public SignalCandle()
            {
                Type = SignalCandleType.Any;
                UpperShadowExpr = BodyExpr = LowerShadowExpr = VolumeExpr = string.Empty;
            }

            public static SignalCandle Parse(string s)
            {
                var candle = new SignalCandle();



                return candle;
            }
        }

        public double ZeroTolerance { get; set; } = 0.0001;
        public string PriceChangeExpectationExpr { get; set; } = "";
        public List<SignalCandle> Candles { get; set; } = new List<SignalCandle>();

        public int CandlesCount
        {
            get
            {
                return Candles.Count;
            }
        }

        public double LastPriceExpectation { get; private set; } = 0.0;

        public bool Verify(List<Candlestick> candles)
        {
            LastPriceExpectation = 0.0;

            if (candles.Count < CandlesCount)
                return false;

            throw new Exception();
        }

        /// <summary>
        /// Format <Signal>{PriceChangeExpectationExpr}Any/Green/Red[UpperShadowExp,BodyExpr,LowerShadowExpr](VolumeExpr)</Signal>
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static Signal Parse(XmlNode node)
        {
            var res = new Signal();
            var s = node.InnerXml.Trim();
            if (s.StartsWith("{"))
            {
                res.PriceChangeExpectationExpr = s.Substring(1, s.IndexOf("}") - 1);
                s = s.Substring(s.IndexOf("}") + 1);
            }

            var candlesS = s.Split(';');
            foreach (var cs in candlesS)
            {
                res.Candles.Add(SignalCandle.Parse(cs));
            }
            return res;
        }
        
    }
}
