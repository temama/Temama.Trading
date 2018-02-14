using NCalc;
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

            public static SignalCandle Parse(string s, int candlePos)
            {
                var candle = new SignalCandle();
                s = s.Replace("\r\n", string.Empty).Trim();

                //  Complete short-form variables
                s = s.Replace("c.us", $"c{candlePos}.us").Replace("c.b", $"c{candlePos}.b").
                    Replace("c.ls", $"c{candlePos}.ls").Replace("c.v", $"c{candlePos}.v").
                    Replace("c.o", $"c{candlePos}.o").Replace("c.c", $"c{candlePos}.c").
                    Replace("c.l", $"c{candlePos}.l").Replace("c.h", $"c{candlePos}.h");

                // Parse shadows & body
                if (s.Contains("[[") && s.Contains("]]"))
                {
                    var expr = s.Substring(s.IndexOf("[["), s.LastIndexOf("]]") + 2 - s.IndexOf("[["));
                    s = s.Replace(expr, string.Empty);
                    expr = expr.Replace("[[", string.Empty).Replace("]]", string.Empty);
                    var splits = expr.Split('|');

                    if (splits.Length != 3)
                        throw new Exception($"Body/Shadows part should consist of 3 parts [[us|b|ls]]. Can't parse {expr}");

                    candle.UpperShadowExpr = CompleteExpr(splits[0].Trim(), "us", candlePos);
                    candle.BodyExpr = CompleteExpr(splits[1].Trim(), "b", candlePos);
                    candle.LowerShadowExpr = CompleteExpr(splits[2].Trim(), "ls", candlePos);
                }

                // Parse volume
                if (s.Contains("(") && s.EndsWith(")"))
                {
                    var expr = s.Substring(s.IndexOf('('));
                    s = s.Replace(expr, string.Empty);
                    candle.VolumeExpr = CompleteExpr(expr.Substring(1, expr.Length - 2).Trim(), "v", candlePos);
                }

                // Parse candle color
                s = s.Trim().ToLower();
                switch (s)
                {
                    case "red":
                    case "r":
                        candle.Type = SignalCandleType.Red;
                        break;
                    case "green":
                    case "g":
                        candle.Type = SignalCandleType.Green;
                        break;
                    case "any":
                    case "a":
                        candle.Type = SignalCandleType.Any;
                        break;
                    default:
                        throw new Exception($"Unknown SignalCandle type {s}");
                }

                return candle;
            }

            private static string CompleteExpr(string exp, string par, int candlePos)
            {
                if (string.IsNullOrEmpty(exp))
                    return exp;

                if (exp.StartsWith("~"))
                {
                    return $"Abs([c{candlePos}.{par}] - {exp.Substring(1)})<[AZ]";
                }
                else if (exp.StartsWith(">") || exp.StartsWith("<") ||
                    exp.StartsWith("=="))
                {
                    return $"[c{candlePos}.{par}]{exp}";
                }

                return exp;
            }
        }

        public double ZeroTolerance { get; set; } = 0.0001;

        public string PriceChangeExpectationExpr { get; set; } = "";

        public List<SignalCandle> Candles { get; set; } = new List<SignalCandle>();

        public string SignalName { get; set; }

        public int CandlesCount
        {
            get
            {
                return Candles.Count;
            }
        }

        public double LastPriceExpectation { get; private set; } = 0.0;

        public bool Verify(List<Candlestick> input)
        {
            LastPriceExpectation = 0.0;

            if (input.Count < CandlesCount)
                return false;

            var workingCandles = new List<Candlestick>();
            for (int i = input.Count - CandlesCount; i < input.Count; i++)
            {
                workingCandles.Add(input[i]);
            }
            
            for (int i = 0; i < CandlesCount; i++)
            {
                if (Candles[i].Type == SignalCandle.SignalCandleType.Green && !workingCandles[i].Green)
                    return false;
                if (Candles[i].Type == SignalCandle.SignalCandleType.Red && workingCandles[i].Green)
                    return false;

                if (!CheckExpression(Candles[i].VolumeExpr, workingCandles) ||
                    !CheckExpression(Candles[i].UpperShadowExpr, workingCandles) ||
                    !CheckExpression(Candles[i].BodyExpr, workingCandles) ||
                    !CheckExpression(Candles[i].LowerShadowExpr, workingCandles))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(PriceChangeExpectationExpr))
            {
                var e = new Expression(PriceChangeExpectationExpr);
                PopulateExpressionParams(e, workingCandles);
                LastPriceExpectation = Convert.ToDouble(e.Evaluate());
            }

            return true;
        }

        private void PopulateExpressionParams(Expression e, List<Candlestick> candles)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                e.Parameters[$"c{i}.v"] = candles[i].Volume;
                e.Parameters[$"c{i}.us"] = candles[i].UpperShadow;
                e.Parameters[$"c{i}.b"] = candles[i].Body;
                e.Parameters[$"c{i}.ls"] = candles[i].LowerShadow;
                e.Parameters[$"c{i}.o"] = candles[i].Open;
                e.Parameters[$"c{i}.c"] = candles[i].Close;
                e.Parameters[$"c{i}.h"] = candles[i].High;
                e.Parameters[$"c{i}.l"] = candles[i].Low;
            }
            e.Parameters["AZ"] = ZeroTolerance;
        }

        private bool CheckExpression(string expr, List<Candlestick> candles)
        {
            if (string.IsNullOrEmpty(expr))
                return true;

            var e = new Expression(expr);
            PopulateExpressionParams(e, candles);
            return Convert.ToBoolean(e.Evaluate());
        }

        /// <summary>
        /// Format <Signal>{PriceChangeExpectationExpr}Any/Green/Red[UpperShadowExp,BodyExpr,LowerShadowExpr](VolumeExpr)</Signal>
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static Signal Parse(XmlNode node)
        {
            var res = new Signal();
            var s = node.InnerText.Trim();
            if (s.StartsWith("{"))
            {
                res.PriceChangeExpectationExpr = s.Substring(1, s.IndexOf("}") - 1);
                s = s.Substring(s.IndexOf("}") + 1);
            }

            var candlesS = s.Split(';');
            var i = 0;
            foreach (var cs in candlesS)
            {
                res.Candles.Add(SignalCandle.Parse(cs, i));
                i++;
            }

            res.SignalName = node.Attributes["name"] != null ? node.Attributes["name"].Value : s;
            return res;
        }

    }
}
