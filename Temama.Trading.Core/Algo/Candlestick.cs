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
              
        
        public TimeSpan Width { get { return End - Start; } }

        public double UpperShadow
        {
            get
            {
                var top = Math.Max(Open, Close);
                return (High - top) / ((High + top) / 2) * 100;
            }
        }

        public double LowerShadow
        {
            get
            {
                var bottom = Math.Min(Open, Close);
                return (Low - bottom) / ((Low + bottom) / 2) * 100;
            }
        }
        
        /// <summary>
        /// Shows price change between open and close in percents (positive or negative)
        /// </summary>
        /// <returns></returns>
        public double Body
        {
            get
            {
                return (Close - Open) / ((Open + Close) / 2) * 100;
            }
        }

        public double Mid
        {
            get
            {
                return (Low + High) / 2;
            }
        }

        public double MidBody
        {
            get
            {
                return (Open + Close) / 2;
            }
        }


        public Candlestick()
        {
            Start = End = DateTime.MinValue;
            Completed = false;
        }

        public Candlestick(DateTime start, DateTime end) : this(start, end, 0)
        {
        }

        public Candlestick(DateTime start, DateTime end, double openPrice)
        {
            Start = start;
            End = end;
            Open = Close = High = Low = openPrice;
            Completed = true;
        }


        public override string ToString()
        {
            return string.Format("[O:{0} H:{1} L:{2} C:{3}]", Open, High, Low, Close);
        }
    }
}
