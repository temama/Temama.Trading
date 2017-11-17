using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;

namespace Temama.Trading.Algo
{
    public class Ranger : Algorithm
    {
        private int _interval = 60;
        private double _priceToSell;
        private double _priceToBuy;

        public override string Name()
        {
            return "Ranger";
        }

        public override void Init(IExchangeApi api, XmlDocument config)
        {
            _api = api;

            var node = config.SelectSingleNode("//TemamaTradingConfig/BaseCurrency");
            _base = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/FundCurrency");
            _fund = node.InnerText;
            node = config.SelectSingleNode("//TemamaTradingConfig/MinBaseToTrade");
            _minBaseToTrade = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/MinFundToTrade");
            _minFundToTrade = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/ExecuteInterval");
            _interval = Convert.ToInt32(node.InnerText);
            node = config.SelectSingleNode("//TemamaTradingConfig/PriceToSell");
            _priceToSell = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            node = config.SelectSingleNode("//TemamaTradingConfig/PriceToBuy");
            _priceToBuy = Convert.ToDouble(node.InnerText, CultureInfo.InvariantCulture);
            _pair = _base + "/" + _fund;
        }

        public void Test()
        {
            //var t = _api.GetMyTrades(_base, _fund);
        }

        public override Task StartTrading()
        {
            if (Trading)
            {
                Logger.Error("Ranger: Trading already in progress");
                return null;
            }

            var task = Task.Run(() =>
            {
                Logger.Info(string.Format("Ranger: Starting trading pair {0}...", _pair.ToUpper()));
                Trading = true;
                while (Trading)
                {
                    DoTradingIteration();
                    Thread.Sleep(_interval * 1000);
                }
            });
            _tradingTask = task;
            return task;
        }

        public override void StopTrading()
        {
            Logger.Info("Ranger: Stopping trading...");
            Trading = false;
            if (_tradingTask != null)
                _tradingTask.Wait();
            _tradingTask = null;
        }


        private void DoTradingIteration()
        {
            try
            {
                PrintSummary();

                MakeDecision();
            }
            catch (Exception ex)
            {
                Logger.Critical("Trading iteration failed. Exception: " + ex.Message);
                AddCritical();
            }
        }

        private void PrintSummary()
        {
            var last = _api.GetLastPrice(_base, _fund);
            Logger.Info(string.Format("Last price: {0}", last));

            var myOrders = _api.GetMyOrders(_base, _fund);
            var sbOrders = new StringBuilder();
            foreach (var order in myOrders)
            {
                Logger.Spam(string.Format("Active order: {0}", order));
                sbOrders.Append(string.Format("{0}:{1}({2}); ", order.Side == "sell" ? "s" : "b", order.Price, order.Volume));
            }
            Logger.Info(string.Format("{0} active orders: {1}", myOrders.Count, sbOrders));

            if (DateTime.Now - _lastFiatBalanceCheckTime > _FiatBalanceCheckInterval)
            {
                CheckFiatBalance(last, myOrders);
            }
        }

        private void MakeDecision()
        {
            var funds = _api.GetFunds(_base, _fund);

            if (funds.Values[_base] > _minBaseToTrade)
            {
                Logger.Info("Ranger: Can place sell order...");
                var order = _api.PlaceOrder(_base, _fund, "sell", GetRoundedSellVolume(GetAlmostAllBases(funds.Values[_base])), _priceToSell);
                NotificationManager.SendImportant(WhoAmI, string.Format("Order placed: {0}", order));
            }

            if (funds.Values[_fund] > _minFundToTrade)
            {
                Logger.Info("Ranger: Can place buy order...");
                var order = _api.PlaceOrder(_base, _fund, "buy", CalculateBuyVolume(_priceToBuy, GetAlmolstAllFunds(funds.Values[_fund])), _priceToBuy);
                NotificationManager.SendImportant(WhoAmI, string.Format("Order placed: {0}", order));
            }
        }
    }
}
