using System;
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
        /// Generates Candlestick ticks. NOTE: it sorts trades by date/time
        /// Max candleWidth = 1d
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="candleWidth"></param>
        /// <returns></returns>
        public static List<Candlestick> TradesToCandles(List<Trade> trades, TimeSpan candleWidth)
        {
            if (candleWidth < TimeSpan.FromSeconds(1))
                throw new Exception(string.Format("TicksToCandles: Too little candle with: {0}", candleWidth));

            if (candleWidth > TimeSpan.FromDays(1))
                throw new Exception(string.Format("TicksToCandles: Too wide candle with: {0}. Use GroupCandlesticks method to gerate wider candlesticks", candleWidth));

            var res = new List<Candlestick>();
            if (trades == null || trades.Count == 0)
                return res;

            trades.Sort(Trade.SortByDate);

            var nextTime = trades[0].CreatedAt.Date;
            while (nextTime < trades[0].CreatedAt)
            {
                nextTime += candleWidth;
            }

            var current = new Candlestick(nextTime - candleWidth, nextTime);
            for (int i = 0; i < trades.Count - 1; i++)
            {
                current.Append(trades[i]);

                if (trades[i + 1].CreatedAt > nextTime)
                {
                    current.Completed = true;
                    res.Add(current);

                    // Checking for flat candles
                    while (nextTime + candleWidth < trades[i + 1].CreatedAt)
                    {
                        res.Add(new Candlestick(nextTime, nextTime + candleWidth, trades[i].Price));
                        nextTime += candleWidth;
                    }

                    current = new Candlestick(nextTime, nextTime + candleWidth);
                    nextTime += candleWidth;
                }
            }

            // ... and for last tick
            current.Append(trades[trades.Count - 1]);
            //TODO: check if completed
            res.Add(current);

            return res;
        }

        public static List<Candlestick> TradesToCandlesNoTime(List<Trade> trades, TimeSpan candleWidth)
        {
            var res = new List<Candlestick>();
            if (trades == null || trades.Count == 0)
                return res;

            var nextTime = trades[0].CreatedAt.Date + candleWidth;
            var current = new Candlestick(trades[0].CreatedAt.Date, nextTime);
            for (int i = 0; i < trades.Count - 1; i++)
            {
                current.Append(trades[i]);

                if (trades[i + 1].CreatedAt > nextTime)
                {
                    current.Completed = true;
                    res.Add(current);

                    // Checking for flat candles
                    while (nextTime + candleWidth < trades[i + 1].CreatedAt)
                    {
                        res.Add(new Candlestick(nextTime, nextTime + candleWidth, trades[i].Price));
                        nextTime += candleWidth;
                    }

                    current = new Candlestick(nextTime, nextTime + candleWidth);
                    nextTime += candleWidth;
                }
            }

            // ... and for last tick
            current.Append(trades[trades.Count - 1]);
            //TODO: check if completed
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
