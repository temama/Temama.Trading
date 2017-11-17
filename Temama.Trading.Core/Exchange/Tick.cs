using System;

namespace Temama.Trading.Core.Exchange
{
    public class Tick
    {
        public DateTime Time { get; set; }
        public double Last { get; set; }
        public double Low { get; set; }
        public double High { get; set; }
    }
}
