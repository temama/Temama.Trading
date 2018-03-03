using NCalc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

                if (!CheckExpression(i, Candles[i].VolumeExpr, workingCandles, input) ||
                    !CheckExpression(i, Candles[i].UpperShadowExpr, workingCandles, input) ||
                    !CheckExpression(i, Candles[i].BodyExpr, workingCandles, input) ||
                    !CheckExpression(i, Candles[i].LowerShadowExpr, workingCandles, input))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(PriceChangeExpectationExpr))
            {
                var e = new Expression(PriceChangeExpectationExpr);
                e.EvaluateParameter += (string name, ParameterArgs args) =>
                {
                    args.Result = EvaluateParameter(name, workingCandles);
                };

                e.EvaluateFunction += (string name, FunctionArgs args) =>
                {
                    args.Result = EvaluateFunction(CandlesCount - 1, name, args, workingCandles, input);
                };
                LastPriceExpectation = Convert.ToDouble(e.Evaluate());
            }

            return true;
        }
        
        private object EvaluateParameter(string name, List<Candlestick> workingCandles)
        {
            // Constants:
            switch (name)
            {
                case "AZ":
                    return ZeroTolerance;
            }

            // Candles values:
            var match = Regex.Match(name, @"c(\d+).(.*)\Z");
            if (match.Success)
            {
                var i = Convert.ToInt32(match.Groups[1].Value);
                var p = match.Groups[2].Value;
                switch (p)
                {
                    case "v": return workingCandles[i].Volume;
                    case "us": return workingCandles[i].UpperShadow;
                    case "b": return workingCandles[i].Body;
                    case "bm": return workingCandles[i].MidBody;
                    case "m": return workingCandles[i].Mid;
                    case "ls": return workingCandles[i].LowerShadow;
                    case "o": return workingCandles[i].Open;
                    case "c": return workingCandles[i].Close;
                    case "l": return workingCandles[i].Low;
                    case "h": return workingCandles[i].High;
                }
            }

            throw new Exception($"Unknown parameter: {name}");
        }

        private object EvaluateFunction(int currentCandle, string name, FunctionArgs args, List<Candlestick> workingCandles, List<Candlestick> inputCandles)
        {
            if (name == "UT" || name == "DT")
            {
                return EvaluateUpDownTrend(currentCandle, name, args, workingCandles, inputCandles);
            }
            //else if (name == "SW")
            //{

            //}

            throw new Exception($"Unknown function: {name}");
        }

        private bool EvaluateUpDownTrend(int currentCandle, string name, FunctionArgs args, List<Candlestick> workingCandles, List<Candlestick> inputCandles)
        {
            var cur = workingCandles[currentCandle];
            var globalIndex = inputCandles.IndexOf(cur);
            var p = (int)args.Parameters[0].Evaluate();
            var ut = name == "UT";
            if (globalIndex - p > 0)
            {
                // Calculating by Moving Avarage
                var sum = 0.0;
                for (int i = globalIndex - p; i < globalIndex; i++)
                {
                    sum += inputCandles[i].Close;
                }
                var ma = sum / p;
                return ut ? cur.Open > ma : cur.Open < ma;
            }
            else
                return false;
        }

        private bool CheckExpression(int currentCandle, string expr, List<Candlestick> workingCandles, List<Candlestick> inputCandles)
        {
            if (string.IsNullOrEmpty(expr))
                return true;

            var e = new Expression(expr);
            //PopulateExpressionParams(e, candles);
            e.EvaluateParameter += (string name, ParameterArgs args) =>
            {
                args.Result = EvaluateParameter(name, workingCandles);
            };

            e.EvaluateFunction += (string name, FunctionArgs args) =>
            {
                args.Result = EvaluateFunction(currentCandle, name, args, workingCandles, inputCandles);
            };
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
