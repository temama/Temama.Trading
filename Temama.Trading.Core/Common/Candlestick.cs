﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Core.Common
{
    public class Candlestick
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Volume { get; set; }

        public bool Completed { get; set; }
        public bool Green
        {
            get
            {
                return Open <= Close;
            }
        }


        public TimeSpan Width { get { return End - Start; } }


        /// <summary>
        /// Percentage of upper shadow price change. Always >= 0
        /// </summary>
        public double UpperShadow
        {
            get
            {
                var top = Math.Max(Open, Close);
                return (High - top) / (top); //* 100;
            }
        }

        /// <summary>
        /// Percentage of lower shadow price change. Always less or equal 0
        /// </summary>
        public double LowerShadow
        {
            get
            {
                var bottom = Math.Min(Open, Close);
                return (Low - bottom) / (bottom); // * 100;
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
                return (Close - Open) / (Open); // * 100;
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
            Open = Close = High = Low = Volume = 0.0;
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
            Volume = 0.0;
            Completed = false;
        }

        public void Append(Tick tick)
        {
            if (Open == 0)
                Open = Close = High = Low = tick.Last;
            else
            {
                Close = tick.Last;
                if (tick.Last > High)
                    High = tick.Last;
                if (tick.Last < Low)
                    Low = tick.Last;
            }
        }

        public void Append(Trade trade)
        {
            if (Open == 0)
                Open = Close = High = Low = trade.Price;
            else
            {
                Volume += trade.Volume;
                Close = trade.Price;
                if (trade.Price > High)
                    High = trade.Price;
                if (trade.Price < Low)
                    Low = trade.Price;
            }
        }

        public override string ToString()
        {
            return $"[O:{Open} H:{High} L:{Low} C:{Close} V:{Volume}]";
        }
    }
}
