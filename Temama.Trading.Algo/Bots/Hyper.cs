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
        private static string _baseUrl = "https://api.coinmarketcap.com/v1/ticker/?limit=";
        private int _topCoins = 10;
        private int _startCoins = 7;
        private int _stopCoins = 5;
        private bool _monitorMode = true;

        private bool _hypeMode = false;
        private Dictionary<string, double> _prev = null;
        private Dictionary<string, int> _raiseMap = null;

        public Hyper(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            _topCoins = Convert.ToInt32(config.GetConfigValue("TopCoins", true, "10"));
            _startCoins = Convert.ToInt32(config.GetConfigValue("StartCoins", true, "7"));
            _stopCoins = Convert.ToInt32(config.GetConfigValue("StopCoins", true, "5"));
            _monitorMode = Convert.ToBoolean(config.GetConfigValue("MonitorMode", true, "true"));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            var latest = GetLatest();
            _log.Info($"Latest stats: {GetDataRepresentation(latest)}");

            if (_prev == null)
            {
                // Very first iteration
                _prev = latest;
                _raiseMap = new Dictionary<string, int>();
                foreach (var kv in latest)
                {
                    _raiseMap.Add(kv.Key, 0);
                }

                return;
            }

            foreach (var kv in latest)
            {
                if (kv.Value > _prev[kv.Key])
                    _raiseMap[kv.Key] = 1;
                else if (kv.Value < _prev[kv.Key])
                    _raiseMap[kv.Key] = -1;
            }

            if (_hypeMode)
            {
                var fallingCount = _raiseMap.Count(kv=> kv.Value == -1);

                // End of hype
                if (fallingCount >= _stopCoins)
                {  
                    OnHypeEnded(latest);
                }
            }
            else
            {
                var raisingCount = latest.Count(kv => kv.Value == 1);
                if (raisingCount >= _startCoins)
                {
                    OnHypeStarted(latest);
                }
            }

            _prev = latest;
        }

        private string GetDataRepresentation(Dictionary<string, double> data)
        {
            return "[" + string.Join("; ", data.Select(kv => $"{kv.Key}:{NumStr(kv.Value)}")) + "]";
        }

        private Dictionary<string, double> GetLatest()
        {
            var res = new Dictionary<string, double>();
            var response = WebApi.Query(_baseUrl + _topCoins);
            var json = JArray.Parse(response);
            foreach (JObject o in json)
            {
                var id = (o["id"] as JValue).Value.ToString();
                var change = Convert.ToDouble((o["percent_change_1h"] as JValue).Value.ToString(), CultureInfo.InvariantCulture);
                res[id] = change;
            }
            return res;
        }

        private void OnHypeStarted(Dictionary<string, double> stats)
        {
            var msg = $"HYPE STARTED with values: {GetDataRepresentation(stats)}\r\n{GetRaiseMapRepresentation()}";
            _hypeMode = true;
            _log.Important(msg);
            NotificationManager.SendImportant(WhoAmI, msg);

            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private void OnHypeEnded(Dictionary<string, double> stats)
        {
            var msg = $"HYPE Ended with values: {GetDataRepresentation(stats)}\r\n{GetRaiseMapRepresentation()}";
            _hypeMode = false;
            _log.Warning(msg);
            NotificationManager.SendWarning(WhoAmI, msg);

            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private string GetRaiseMapRepresentation()
        {
            return "[" + string.Join("; ", _raiseMap.Select(kv =>
                $"<font color='{(kv.Value > 0 ? "green" : "red")}'>{kv.Key} {RaiseRepresentaion(kv.Value)}</font>")) + "]";
        }

        private string RaiseRepresentaion(int val)
        {
            return val > 0 ? "▲" : (val < 0 ? "▼" : "-");
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
