﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Algo.Bots
{
    public class Hyper : TradingBot
    {
        private static string _baseUrl = "https://api.coinmarketcap.com/v1/ticker/?limit=";
        private int _topCoins = 10;
        private int _stopCoins = 5;
        private double _hypePercent = 0.3;
        private double _stopPercent = 0.1;
        private bool _monitorMode = true;

        private bool _hypeMode = false;

        public Hyper(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            _topCoins = Convert.ToInt32(config.GetConfigValue("TopCoins", true, "10"));
            _stopCoins = Convert.ToInt32(config.GetConfigValue("StopCoins", true, "3"));
            _hypePercent = Convert.ToDouble(config.GetConfigValue("HypePercent", true, "0.3"), CultureInfo.InvariantCulture);
            _stopPercent = Convert.ToDouble(config.GetConfigValue("StopPercent", true, "0.3"), CultureInfo.InvariantCulture);
            _monitorMode = Convert.ToBoolean(config.GetConfigValue("MonitorMode", true, "true"));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            var latest = GetLatest();

            _log.Info($"Latest stats: {GetDataRepresentation(latest)}");

            if (_hypeMode)
            {
                var fallingCount = latest.Count(kv=> kv.Value < _stopPercent);

                // End of hype
                if (fallingCount >= _stopCoins)
                {
                    var msg = $"HYPE Ended with values: {GetDataRepresentation(latest)}";
                    _hypeMode = false;
                    _log.Warning(msg);
                    NotificationManager.SendWarning(WhoAmI, msg);
                    DoSell();
                }
            }
            else
            {
                var startOfHype = latest.All(kv => kv.Value >= _hypePercent);
                if (startOfHype)
                {
                    var msg = $"HYPE STARTED with values: {GetDataRepresentation(latest)}";
                    _hypeMode = true;
                    _log.Important(msg);
                    NotificationManager.SendImportant(WhoAmI, msg);
                }
            }
        }

        private string GetDataRepresentation(Dictionary<string, double> latest)
        {
            return $"[{string.Join(";", latest.Select(kv => kv.Key + ":" + kv.Value))}]";
        }

        private Dictionary<string, double> GetLatest()
        {
            var res = new Dictionary<string, double>();
            var response = WebApi.Query(_baseUrl + _topCoins);
            var json = JArray.Parse(response);
            foreach (JObject o in json)
            {
                res[(o["id"] as JValue).Value.ToString()] = Convert.ToDouble((o["percent_change_1h"] as JValue).Value.ToString(), CultureInfo.InvariantCulture);
            }
            return res;
        }

        private void DoBuy()
        {
            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private void DoSell()
        {
            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        public override void Emulate(DateTime start, DateTime end)
        {
            // Coinmarketcap is not supported historical data so far
            throw new Exception("Hyper emulation is not supported");
        }

        protected override string WhoAmIValue()
        {
            return "Hyper" + (_monitorMode ? " (monitor)" : "on many");
        }

        protected override void PrintSummary()
        {
            // Do nothing
        }
    }
}
