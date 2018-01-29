using System;
using System.Globalization;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo
{
    public class Ranger : TradingBot
    {
        private double _priceToSell;
        private double _priceToBuy;
        
        public override string Name()
        {
            return "Ranger";
        }

        public Ranger(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        { }

        protected override void InitAlgo(XmlNode config)
        {
            _priceToSell = Convert.ToDouble(config.GetConfigValue("PriceToSell"), CultureInfo.InvariantCulture);
            _priceToBuy = Convert.ToDouble(config.GetConfigValue("PriceToBuy"), CultureInfo.InvariantCulture);
        }
        
        protected override void TradingIteration(DateTime dateTime)
        {
            if (!_iterationStatsUpdated)
                UpdateIterationStats();

            var allowedBase = GetLimitedBaseAmount();
            var allowedFund = GetLimitedFundsAmount();
            if (allowedBase > _minBaseToTrade)
            {
                _log.Info("Ranger: Can place sell order...");
                var order = _api.PlaceOrder(_base, _fund, "sell", 
                    _api.GetRoundedSellVolume(GetAlmolstAll(allowedBase)), _priceToSell);
                NotifyOrderPlaced(order);
            }

            if (allowedFund > _minFundToTrade)
            {
                var amount = _api.CalculateBuyVolume(_priceToBuy, GetAlmolstAll(allowedFund));
                if (amount > _minBaseToTrade)
                {
                    _log.Info("Ranger: Can place buy order...");
                    var order = _api.PlaceOrder(_base, _fund, "buy", amount, _priceToBuy);
                    NotifyOrderPlaced(order);
                }
            }
        }
    }
}
