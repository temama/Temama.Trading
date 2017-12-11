using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Algo
{
    public class Candlestick
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public bool Completed { get; set; }

        public Candlestick()
        {
            Start = End = DateTime.MinValue;
        }

        public Candlestick(DateTime start, DateTime end) : this(start, end, 0)
        {
        }

        public Candlestick(DateTime start, DateTime end, double openPrice)
        {
            Start = start;
            End = end;
            Open = Close = High = Low = openPrice;
        }

        /// <summary>
        /// Time the candle shows in seconds
        /// </summary>
        public int ShowTime()
        {
            return (int)(End - Start).TotalSeconds;
        }

        public double UpperShadow()
        {
            var top = Math.Max(Open, Close);
            return (High - top) / ((High + top) / 2) * 100;
        }

        public double LowerShadow()
        {
            var bottom = Math.Min(Open, Close);
            return (Low - bottom) / ((Low + bottom) / 2) * 100;
        }

        /// <summary>
        /// Shows price change between open and close in percents (positive or negative)
        /// </summary>
        /// <returns></returns>
        public double Body()
        {
            return (Close - Open) / ((Open + Close) / 2) * 100;
        }
    }
}
