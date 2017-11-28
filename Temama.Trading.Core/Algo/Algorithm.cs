using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Notifications;

namespace Temama.Trading.Core.Algo
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

        public string WhoAmI
        {
            get
            {
                return string.Format("{0} on {1}", Name(), _api.Name());
            }
        }

        public virtual string Name()
        {
            return "Algorithm";
        }

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

            Logger.Logger.Info(string.Format("--------------- Fiat amount: {0} [{1}]-----------------", sum, profit));
            NotificationManager.SendInfo(WhoAmI, string.Format("Fiat: {0} [{1}]", sum, profit));

            _lastFiatBalance = sum;
            _lastFiatBalanceCheckTime = DateTime.Now;
            return sum;
        }

        protected virtual double CalculateBuyVolume(double price, double fund)
        {
            return Math.Round(Math.Floor(fund) / price, 5);
        }

        protected virtual double GetRoundedSellVolume(double vol)
        {
            return Math.Round(vol, 5);
        }

        protected virtual double GetAlmolstAllFunds(double funds)
        {
            return funds - funds * 0.01;
        }

        protected virtual double GetAlmostAllBases(double amount)
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
                Logger.Logger.Critical("Criticals count exceeded maximum. Something goes wrong. Will stop trading");
                NotificationManager.SendError(WhoAmI, "Exceeded max criticals. Stopping...");
                StopTrading();
            }
        }
    }
}
