using System;

namespace Temama.Trading.Core.Exchange
{
    public class Tick
    {
        public DateTime Time { get; set; }
        public double Last { get; set; }
        
        public static int DateTimeAscSorter(Tick first, Tick second)
        {
            if (first.Time < second.Time)
                return -1;
            else if (second.Time < first.Time)
                return 1;
            else return 0;
        }
    }
}
