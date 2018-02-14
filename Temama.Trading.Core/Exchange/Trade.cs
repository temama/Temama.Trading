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
        public DateTime CreatedAt { get; set; }
        public string Side { get; set; }

        /// <summary>
        /// Sorts by date asc
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static int SortByDate(Trade first, Trade second)
        {
            if (first.CreatedAt < second.CreatedAt)
                return -1;
            else if (second.CreatedAt < first.CreatedAt)
                return 1;
            else return 0;
        }

        /// <summary>
        /// Sorts by date desc
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static int SortByDateDesc(Trade first, Trade second)
        {
            if (first.CreatedAt < second.CreatedAt)
                return 1;
            else if (second.CreatedAt < first.CreatedAt)
                return -1;
            else return 0;
        }

        public static int SortByPrice(Trade first, Trade second)
        {
            if (first.Price < second.Price)
                return -1;
            else if (second.Price < first.Price)
                return 1;
            else return 0;
        }

        public Trade Clone()
        {
            return new Trade
            {
                Id = Id,
                Pair = Pair,
                Price = Price,
                Volume = Volume,
                CreatedAt = CreatedAt,
                Side = Side
            };
        }

        public override string ToString()
        {
            return string.Format("#{0}:{1}:{2}:{3}:{4}", Id, Pair, Side, Volume, Price);
        }
    }
}
