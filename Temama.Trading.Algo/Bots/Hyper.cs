using Newtonsoft.Json.Linq;
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
        private class CoinStat
        {
            public string Id { get; set; }
            public double PriceChange { get; set; }
            public DateTime TimeStamp { get; set; }
            public double Delta { get; set; }
        }

        private static string _baseUrl = "https://api.coinmarketcap.com/v1/ticker/?limit=";
        private int _topCoins = 10;
        private int _stopCoins = 5;
        private double _hypePercent = 0.01;
        private double _stopPercent = 0.0;
        private bool _monitorMode = true;

        private Dictionary<string, CoinStat> _stats = null;
        private bool _hypeMode = false;

        public Hyper(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            _topCoins = Convert.ToInt32(config.GetConfigValue("TopCoins", true, "10"));
            _stopCoins = Convert.ToInt32(config.GetConfigValue("StopCoins", true, "3"));
            _hypePercent = Convert.ToDouble(config.GetConfigValue("HypePercent", true, "0.01"), CultureInfo.InvariantCulture);
            _stopPercent = Convert.ToDouble(config.GetConfigValue("StopPercent", true, "0.0"), CultureInfo.InvariantCulture);
            _monitorMode = Convert.ToBoolean(config.GetConfigValue("MonitorMode", true, "true"));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            var latest = GetLatest();
            
            if (_stats == null || latest.Keys.Any(k => !_stats.Keys.Contains(k)))
            {
                _stats = latest;
                _log.Info($"Latest stats: {GetDataRepresentation(_stats)}");
                if (_hypeMode)
                    OnHypeEnded();

                return;
            }

            foreach (var c in latest)
            {
                if (c.Value.TimeStamp > _stats[c.Key].TimeStamp)
                {
                    var sc = _stats[c.Key];
                    sc.Delta = c.Value.PriceChange - sc.PriceChange;
                    sc.PriceChange = c.Value.PriceChange;
                    sc.TimeStamp = c.Value.TimeStamp;
                }
            }

            _log.Info($"Latest stats: {GetDataRepresentation(_stats)}");

            if (_hypeMode)
            {
                var fallingCount = _stats.Count(kv=> kv.Value.Delta < _stopPercent);

                // End of hype
                if (fallingCount >= _stopCoins)
                {  
                    OnHypeEnded();
                }
            }
            else
            {
                var startOfHype = _stats.All(kv => kv.Value.Delta >= _hypePercent);
                if (startOfHype)
                {
                    OnHypeStarted();
                }
            }
        }

        private string GetDataRepresentation(Dictionary<string, CoinStat> data)
        {
            return "[" +
                string.Join("; ", data.Select(kv => $"{kv.Key}:{NumStr(kv.Value.PriceChange)}[{NumStr(kv.Value.Delta)}]"))
                + "]";
        }

        private Dictionary<string, CoinStat> GetLatest()
        {
            var res = new Dictionary<string, CoinStat>();
            var response = WebApi.Query(_baseUrl + _topCoins);
            var json = JArray.Parse(response);
            foreach (JObject o in json)
            {
                var id = (o["id"] as JValue).Value.ToString();
                var change = Convert.ToDouble((o["percent_change_1h"] as JValue).Value.ToString(), CultureInfo.InvariantCulture);
                var time = UnixTime.FromUnixTime(Convert.ToInt64((o["last_updated"] as JValue).Value));
                res[id] = new CoinStat
                {
                    Id = id,
                    TimeStamp = time,
                    PriceChange = change,
                    Delta = 0
                };
            }
            return res;
        }

        private void OnHypeStarted()
        {
            var msg = $"HYPE STARTED with values: {GetDataRepresentation(_stats)}";
            _hypeMode = true;
            _log.Important(msg);
            NotificationManager.SendImportant(WhoAmI, msg);

            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private void OnHypeEnded()
        {
            var msg = $"HYPE Ended with values: {GetDataRepresentation(_stats)}";
            _hypeMode = false;
            _log.Warning(msg);
            NotificationManager.SendWarning(WhoAmI, msg);

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
