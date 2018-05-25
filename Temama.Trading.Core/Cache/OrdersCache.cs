using System;
using System.Collections.Generic;
using System.Text;
using Temama.Trading.Core.Exchange;

namespace Temama.Trading.Core.Cache
{
    public class OrdersCache
    {
        public enum OrdersCacheMode
        {
            None,
            Memory,
            Database,
            Both
        }

        public string Name { get; private set; } = "General";

        public OrdersCacheMode Mode { get; private set; }

        public OrdersCache(OrdersCacheMode mode)
        {
        }

        public OrdersCache(string name)
        {
            Name = name;
        }

        public void Add(Order order)
        {
            if (Mode == OrdersCacheMode.None)
                return;
        }

        public Order Get(string orderId)
        {
            if (Mode == OrdersCacheMode.None)
                return null;
            return null;
        }

        public List<Order> GetAll()
        {
            if (Mode == OrdersCacheMode.None)
                return new List<Order>();
            return null;
        }

        public void Remove(Order order)
        {
            Remove(order.Id);
        }

        public void Remove(string orderId)
        {

        }

        public void Sync(List<Order> orders)
        {
            // remove from cache orders which are not in "orders"
        }

        public static OrdersCacheMode ParseMode(string mode)
        {
            switch (mode.ToLower())
            {
                case "memory":
                    return OrdersCacheMode.Memory;
                case "db":
                case "database":
                    return OrdersCacheMode.Database;
                case "both":
                    return OrdersCacheMode.Both;
                default:
                    return OrdersCacheMode.None;
            }
        }
    }
}
