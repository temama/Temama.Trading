using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public class Trade
    {
        public string Id { get; set; }
        public string Pair { get; set; }
        public double Price { get; set; }
        public double Volume { get; set; }
        public double Funds { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Side { get; set; }

        /// <summary>
        /// Sorts by date desc
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static int SortByDate(Trade first, Trade second)
        {
            if (first.CreatedAt < second.CreatedAt)
                return 1;
            else if (second.CreatedAt < first.CreatedAt)
                return -1;
            else return 0;
        }

        public override string ToString()
        {
            return string.Format("#{0}:{1}:{2}:{3}:{4}", Id, Pair, Side, Volume, Price);
        }
    }
}
