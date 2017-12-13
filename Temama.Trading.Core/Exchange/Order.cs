using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public class Order
    {
        public string Id { get; set; }
        public string Pair { get; set; }
        public string Side { get; set; }
        public double Price { get; set; }
        public double Volume { get; set; }
        public DateTime CreatedAt { get; set; }

        public Order Clone()
        {
            return new Order()
            {
                Id = this.Id,
                CreatedAt = this.CreatedAt,
                Pair = this.Pair,
                Price = this.Price,
                Side = this.Side,
                Volume = this.Volume
            };
        }

        public static int SortByPrice(Order first, Order second)
        {
            if (first.Price < second.Price)
                return -1;
            else if (second.Price < first.Price)
                return 1;
            else return 0;
        }

        public static int SortByPriceDesc(Order first, Order second)
        {
            if (first.Price < second.Price)
                return 1;
            else if (second.Price < first.Price)
                return -1;
            else return 0;
        }

        public override string ToString()
        {
            return string.Format("#{0}:{1}:{2}:{3}:{4}", Id, Pair, Side, Price, Volume);
        }
    }
}
