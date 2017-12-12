﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Core.Algo
{
    public class CandlestickHelper
    {

        /// <summary>
        /// Generates Candlestick ticks. NOTE: it sorts ticks by date/time
        /// Max candleWidth = 1d
        /// </summary>
        /// <param name="ticks"></param>
        /// <param name="candleWidth"></param>
        /// <returns></returns>
        public static List<Candlestick> TicksToCandles(List<Tick> ticks, TimeSpan candleWidth)
        {
            if (candleWidth < TimeSpan.FromSeconds(1))
                throw new Exception(string.Format("TicksToCandles: Too little candle with: {0}", candleWidth));

            if (candleWidth > TimeSpan.FromDays(1))
                throw new Exception(string.Format("TicksToCandles: Too wide candle with: {0}. Use GroupCandlesticks method to gerate wider candlesticks", candleWidth));

            var res = new List<Candlestick>();
            if (ticks == null || ticks.Count == 0)
                return res;

            ticks.Sort(Tick.DateTimeAscSorter);

            var nextTime = ticks[0].Time.Date;
            while (nextTime < ticks[0].Time)
            {
                nextTime += candleWidth;
            }

            var current = new Candlestick(nextTime - candleWidth, nextTime, ticks[0].Last);
            var price = 0.0;
            for (int i = 0; i < ticks.Count - 1; i++)
            {
                price = ticks[i].Last;
                if (price > current.High)
                    current.High = price;
                if (price < current.Low)
                    current.Low = price;

                if (ticks[i + 1].Time > nextTime)
                {
                    current.Close = ticks[i].Last;
                    res.Add(current);

                    // Checking for flat candles
                    while (nextTime + candleWidth < ticks[i + 1].Time)
                    {
                        res.Add(new Candlestick(nextTime, nextTime + candleWidth, ticks[i].Last));
                        nextTime += candleWidth;
                    }

                    current = new Candlestick(nextTime, nextTime + candleWidth, ticks[i + 1].Last);
                    nextTime += candleWidth;
                }
            }

            // ... and for last tick
            price = ticks[ticks.Count - 1].Last;
            if (price > current.High)
                current.High = price;
            if (price < current.Low)
                current.Low = price;

            res.Add(current);

            return res;
        }

        /// <summary>
        /// Group same-width candles into wider
        /// </summary>
        /// <param name="candlesticks"></param>
        /// <param name="resultWidth"></param>
        /// <returns></returns>
        public static List<Candlestick> GroupCandlesticks(List<Candlestick> candles, int candlesInGroup)
        {
            var res = new List<Candlestick>();
            if (candles == null || candles.Count == 0)
                return res;

            var first = candles[0];
            var expWidth = first.Width;
            var current = new Candlestick(first.Start, first.Start.AddSeconds(candlesInGroup * expWidth.TotalSeconds), first.Open)
            {
                High = first.High,
                Low = first.Low,
                Close = first.Close
            };
            for (int i = 1; i < candles.Count; i++)
            {
                if ((int)candles[i].Width.TotalSeconds != (int)expWidth.TotalSeconds)
                    throw new Exception("GroupCandlesticks: All candles expected to be same-with'ed: " + expWidth);

                if (candles[i].High > current.High)
                    current.High = candles[i].High;
                if (candles[i].Low < current.Low)
                    current.Low = candles[i].Low;
                current.Close = candles[i].Close;

                if ((i + 1) % candlesInGroup == 0)
                {
                    res.Add(current);

                    if (i != candles.Count - 1)
                    {
                        current = new Candlestick(candles[i + 1].Start, candles[i + 1].Start.AddSeconds(candlesInGroup * expWidth.TotalSeconds), candles[i + 1].Open);
                    }
                }
            }

            if (!res.Contains(current))
            {
                res.Add(current);
            }

            return res;
        }
    }
}
