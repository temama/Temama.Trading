using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Exchanges.Emu
{
    public class EmuApi : ExchangeApi, IExchangeAnalitics, IExchangeEmulator
    {
        private DateTime _lastTime = DateTime.MinValue;
        private int _currentTick = 0;
        private DateTime _ticksStartDate = DateTime.MinValue;
        private DateTime _ticksEndDate = DateTime.MinValue;
        private Funds _userFunds = new Funds();
        private double _takerFee = 0.0;
        private double _makerFee = 0.0;
        private int _nonceId = 100;
        private string _base;
        private string _fund;

        private List<Tick> _ticks = new List<Tick>();
        private List<Order> _userOrders = new List<Order>();
        private List<Trade> _userTrades = new List<Trade>();

        public EmuApi(XmlNode config, Logger logger) : base(config, logger)
        { }

        protected override void Init(XmlNode config)
        {
            _publicOnly = false;
            _ticks = new List<Tick>();
            _userOrders = new List<Order>();
            _userTrades = new List<Trade>();
            
            var historicalFile = config.GetConfigValue("HistoricalFile"); 
            _takerFee = Convert.ToDouble(config.GetConfigValue("TakerFee"), CultureInfo.InvariantCulture) * 0.01;
            _makerFee = Convert.ToDouble(config.GetConfigValue("MakerFee"), CultureInfo.InvariantCulture) * 0.01;
            var node = config.SelectSingleNode("InitialFunds");
            foreach (XmlNode fund in node.ChildNodes)
            {
                var curr = fund.Name;
                var value = Convert.ToDouble(fund.InnerText, CultureInfo.InvariantCulture);
                _userFunds.Values.Add(curr, value);
            }

            _log.Important("EmuApi.Init: Loading data from " + historicalFile);
            LoadHistoricalData(historicalFile);

            _log.Important($"EmuApi.Init: Loaded data from {_ticksStartDate} to {_ticksEndDate}");
        }
        
        public void SetIterationTime(DateTime time)
        {
            if (time < _ticksStartDate && time > _ticksEndDate)
                throw new Exception("EmuApi.SetIterationTime: Provided time outside emulation range " + time);

            if (time < _lastTime)
                throw new Exception("EmuApi.SetIterationTime: Provided time " + time + "less then previous iteration time" + _lastTime);

            EmulateExchangeOperations(time);
            _lastTime = time;
        }

        public void EmulateExchangeOperations(DateTime byTime)
        {
            for (int i = _currentTick; i < _ticks.Count-1; i++)
            {
                ProcessUserOrders(i);

                if (_ticks[i+1].Time > byTime)
                {
                    _currentTick = i;
                    break;
                }
            }

            if (_currentTick >= _ticks.Count - 1)
            {
                _log.Info("Current funds: " + _userFunds);
                throw new Exception("EmuApi.EmulateExchangeOperations: Exceeded end of emulation range");
            }
        }

        public override string Name()
        {
            return "Emulator";
        }

        public override double GetLastPrice(string baseCur, string fundCur)
        {
            SetBaseFund(baseCur, fundCur);
            return _ticks[_currentTick].Last;
        }

        protected override void CancellOrderImpl(Order order)
        {
            var userOrder = _userOrders.FirstOrDefault(o => o.Id == order.Id);
            if (userOrder == null)
                throw new Exception("EmuApi.CancellOrder: No orders with id=" + order.Id);

            if (userOrder.Side == "buy")
            {
                _userFunds.Values[_fund] += userOrder.Price * userOrder.Volume;
            }
            else
            {
                _userFunds.Values[_base] += userOrder.Volume;
            }
            _userOrders.Remove(userOrder);
        }

        protected override Funds GetFundsImpl(string baseCur, string fundCur)
        {
            SetBaseFund(baseCur, fundCur);
            var funds = new Funds();
            foreach (var f in _userFunds.Values)
            {
                funds.Values.Add(f.Key, f.Value);
            }
            return funds;
        }

        protected override List<Order> GetMyOrdersImpl(string baseCur, string fundCur)
        {
            SetBaseFund(baseCur, fundCur);
            var res = new List<Order>();
            foreach (var order in _userOrders)
            {
                res.Add(order.Clone());            
            }
            return res;
        }

        protected override List<Trade> GetMyTradesImpl(string baseCur, string fundCur)
        {
            SetBaseFund(baseCur, fundCur);
            var res = new List<Trade>();
            foreach (var t in _userTrades)
            {
                res.Add(new Trade()
                {
                    Id = t.Id,
                    CreatedAt = t.CreatedAt,
                    Funds = t.Funds,
                    Pair = t.Pair,
                    Price = t.Price,
                    Side = t.Side,
                    Volume = t.Volume
                });
            }
            return res;
        }

        /// <summary>
        /// Order book is not actually historical. 
        /// It returns 20 buy & 20 sell orders with price step +-0.1% and random volume
        /// </summary>
        /// <param name="baseCur"></param>
        /// <param name="fundCur"></param>
        /// <returns></returns>
        public override OrderBook GetOrderBook(string baseCur, string fundCur)
        {
            SetBaseFund(baseCur, fundCur);
            var book = new OrderBook();
            var price = _ticks[_currentTick].Last;
            var rand = new Random(DateTime.Now.Millisecond);
            for (int i = 0; i < 20; i++)
            {
                book.Asks.Add(new Order()
                {
                    Id = "-1",
                    CreatedAt = _lastTime,
                    Pair = baseCur + fundCur,
                    Side = "sell",
                    Price = price + i * 0.001,
                    Volume = rand.Next(1000) * 0.01
                });
                book.Bids.Add(new Order()
                {
                    Id = "-1",
                    CreatedAt = _lastTime,
                    Pair = baseCur + fundCur,
                    Side = "buy",
                    Price = price - i * 0.001,
                    Volume = rand.Next(1000) * 0.01
                });
            }
            return book;
        }

        public List<Tick> GetRecentPrices(string baseCur, string fundCur, DateTime fromDate, int maxResultCount = 1000)
        {
            SetBaseFund(baseCur, fundCur);
            var res = new List<Tick>();
            for (int i = _currentTick; i > _currentTick - maxResultCount; i--)
            {
                var t = _ticks[i];
                if (i <= 0 || t.Time < fromDate)
                    break;
                res.Add(new Tick()
                {
                    Time = t.Time,
                    Last = t.Last
                });
            }

            return res;
        }

        protected override Order PlaceOrderImpl(string baseCur, string fundCur, string side, double volume, double price)
        {
            SetBaseFund(baseCur, fundCur);
            if (side == "buy")
            {
                if (_userFunds.Values[fundCur] < price * volume)
                    throw new Exception("EmuApi.PlaceOrder: Not enough funds to place order");
                else
                    _userFunds.Values[fundCur] -= price * volume;
            }

            if (side == "sell")
            {
                if (_userFunds.Values[baseCur] < volume)
                    throw new Exception("EmuApi.PlaceOrder: Not enough " + baseCur + " to place order");
                else
                    _userFunds.Values[baseCur] -= volume;
            }

            var res = new Order()
            {
                Id = GetSomeId().ToString(),
                CreatedAt = _lastTime,
                Pair = baseCur + fundCur,
                Price = price,
                Volume = volume,
                Side = side
            };
            _userOrders.Add(res);

            if ((side == "sell" && res.Price <= _ticks[_currentTick].Last) ||
                (side == "buy" && res.Price >= _ticks[_currentTick].Last))
            {
                CompleteUserOrder(res, _lastTime, true);
                _userOrders.Remove(res);
            }

            return res.Clone();
        }

        private void SetBaseFund(string baseCur, string fundCur)
        {
            _base = baseCur;
            _fund = fundCur;
        }

        private int GetSomeId()
        {
            return ++_nonceId;
        }
        
        private void ProcessUserOrders(int tickNumber)
        {
            if (_userOrders.Count == 0)
                return;

            var price = _ticks[tickNumber].Last;
            var ordersToRemove = new List<Order>();
            foreach (var order in _userOrders)
            {
                if ((order.Side == "buy" && price <= order.Price) ||
                    (order.Side == "sell" && price >= order.Price))
                {
                    CompleteUserOrder(order, _ticks[tickNumber].Time, false);
                    ordersToRemove.Add(order);
                }
            }

            if (ordersToRemove.Count > 0)
            {
                foreach (var order in ordersToRemove)
                {
                    _userOrders.Remove(order);
                }
            }
        }

        private void CompleteUserOrder(Order order, DateTime dateTime, bool isMarket)
        {
            double amount;
            if (order.Side == "buy")
            {
                amount = order.Volume;
                amount -= amount * (isMarket ? _takerFee : _makerFee);
                _userFunds.Values[_base] += amount;
            }
            else
            {
                amount = order.Price * order.Volume;
                amount -= amount * (isMarket ? _takerFee : _makerFee);
                _userFunds.Values[_fund] += amount;
            }

            var trade = new Trade()
            {
                Id = GetSomeId().ToString(),
                CreatedAt = dateTime,
                Side = order.Side,
                Price = order.Price,
                Volume = order.Volume,
                Funds = amount,
                Pair = _base + _fund
            };
            _userTrades.Add(trade);
        }

        private void LoadHistoricalData(string fileName)
        {
            foreach (var line in File.ReadLines(fileName))
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var jLine = JObject.Parse(line);
                _ticks.Add(new Tick()
                {
                    Time = DateTime.Parse((jLine["Time"] as JValue).Value.ToString()),
                    Last = Convert.ToDouble((jLine["Last"] as JValue).Value, CultureInfo.InvariantCulture)
                });
            }

            _ticks.Sort(Tick.DateTimeAscSorter);
            _ticksStartDate = _ticks[0].Time;
            _ticksEndDate = _ticks[_ticks.Count - 1].Time;
        }
    }
}
