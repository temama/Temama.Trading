using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;

namespace Temama.Trading.Algo
{
    public abstract class Algorithm
    {
        protected int _maxCriticalsCount = 50;
        protected string _base;
        protected string _fund;
        protected TimeSpan _marketOrderFillingTimeout = TimeSpan.FromMinutes(1);

        public bool Trading { get; protected set; }
        protected Task _tradingTask;
        protected DateTime _lastFiatBalanceCheckTime = DateTime.MinValue;
        protected TimeSpan _FiatBalanceCheckInterval = TimeSpan.FromMinutes(10);
        protected double _lastFiatBalance = 0;
        
        protected double _minBaseToTrade;
        protected double _minFundToTrade;

        protected string _pair;
        protected IExchangeApi _api;
        protected int _criticalsCount = 0;

        public abstract void Init(IExchangeApi api, XmlDocument config);

        public abstract Task StartTrading();

        public abstract void StopTrading();

        protected double CheckFiatBalance(double last, List<Order> myOrders)
        {
            var funds = _api.GetFunds(_base, _fund);
            var sum = funds.Values[_fund] + funds.Values[_base] * last;
            foreach (var order in myOrders)
            {
                if (order.Side == "sell")
                    sum += order.Volume * last;
                else
                    sum += order.Volume * order.Price;
            }
            var profit = "?";
            if (_lastFiatBalance != 0)
            {
                profit = Math.Round(sum - _lastFiatBalance, 5).ToString();
                if (!profit.StartsWith("-"))
                    profit = "+" + profit;
            }

            Logger.Info(string.Format("--------------- Fiat amount: {0} [{1}]-----------------", sum, profit));

            _lastFiatBalance = sum;
            _lastFiatBalanceCheckTime = DateTime.Now;
            return sum;
        }

        protected double CalculateBuyVolume(double price, double fund)
        {
            return Math.Round(Math.Floor(fund) / price, 5);
        }

        protected double GetRoundedSellVolume(double vol)
        {
            return Math.Round(vol, 5);
        }

        protected double GetAlmolstAllFunds(double funds)
        {
            return funds - funds * 0.01;
        }

        protected double GetAlmostAllBases(double amount)
        {
            return amount - amount * 0.01;
        }

        /// <summary>
        /// Kind of protection
        /// </summary>
        protected void AddCritical()
        {
            _criticalsCount++;
            if (_criticalsCount >= _maxCriticalsCount)
            {
                Logger.Critical("Sheriff: Criticals count exceeded maximum. Something goes wrong. Will stop trading");
                StopTrading();
            }
        }
    }
}
