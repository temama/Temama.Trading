using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Common;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;
using Temama.Trading.Core.Web;

namespace Temama.Trading.Algo.Bots
{
    public class Hyper : TradingBot
    {
        private class ToDo
        {

            public static ToDo Parse(Hyper bot, XmlNode node)
            {
                throw new NotImplementedException();
            }
        }

        private Dictionary<Pair, Queue<double>> _prices = new Dictionary<Pair, Queue<double>>();
        private List<ToDo> _onHypeStart = new List<ToDo>();
        private List<ToDo> _onHypeEnd = new List<ToDo>();

        private bool _monitorMode = true;
        private int _iterationsToCheck = 10;

        // Stubbing this so far
        private int _startCoins { get { return _prices.Count; } }
        private int _stopCoins { get { return 1; } }

        private bool _hypeMode = false;


        public Hyper(XmlNode config, ILogHandler logHandler) : base(config, logHandler)
        {
        }

        protected override void InitAlgo(XmlNode config)
        {
            _prices.Clear();
            _onHypeStart.Clear();
            _onHypeEnd.Clear();

            var checkPairs = config.SelectNodes("CheckPairs/Pair");
            foreach (XmlNode node in checkPairs)
            {
                _prices.Add(Pair.Parse(node), new Queue<double>());
            }

            if (_prices.Count == 0)
                throw new Exception("Should be provided at least 1 pair to chek");

            var toDoNodes = config.SelectNodes("OnHypeStarts/ToDo");
            foreach (XmlNode node in toDoNodes)
            {
                _onHypeStart.Add(ToDo.Parse(this, node));
            }

            toDoNodes = config.SelectNodes("OnHypeEnds/ToDo");
            foreach (XmlNode node in toDoNodes)
            {
                _onHypeEnd.Add(ToDo.Parse(this, node));
            }

            _iterationsToCheck = Convert.ToInt32(config.GetConfigValue("IterationsToCheck", true, "10"));
            _monitorMode = Convert.ToBoolean(config.GetConfigValue("MonitorMode", true, "false"));
        }

        protected override void TradingIteration(DateTime dateTime)
        {
            foreach (var p in _prices.Keys)
            {
                var price = _api.GetLastPrice(p.Base, p.Fund);
                _prices[p].Enqueue(price);
            }

            if (_prices.First().Value.Count < _iterationsToCheck)
            {
                _log.Info("Collecting prices info...");
                return;
            }

            var stats = new Dictionary<string, double>();
            foreach (var p in _prices)
            {
                var diff = p.Value.Last() - p.Value.First();
                stats.Add(p.Key.ToString(), diff);
            }
            
            _log.Info($"Latest stats: {GetDataRepresentation(stats)}");
            
            if (_hypeMode)
            {
                var fallingCount = stats.Count(kv=> kv.Value < 0);

                // End of hype
                if (fallingCount >= _stopCoins)
                {  
                    OnHypeEnded(stats);
                }
            }
            else
            {
                var raisingCount = stats.Count(kv => kv.Value > 0);
                if (raisingCount >= _startCoins)
                {
                    OnHypeStarted(stats);
                }
            }
            
            foreach (var p in _prices)
            {
                p.Value.Dequeue();
            }
        }
                
        private void OnHypeStarted(Dictionary<string, double> stats)
        {
            _hypeMode = true;
            var msg = $"HYPE STARTED with values: ";
            _log.Important(msg + GetDataRepresentation(stats));
            NotificationManager.SendImportant(WhoAmI, msg + GetRaiseMapRepresentation(stats));

            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private void OnHypeEnded(Dictionary<string, double> stats)
        {
            _hypeMode = false;
            var msg = $"HYPE ENDED with values: ";
            _log.Info(msg + GetDataRepresentation(stats));
            NotificationManager.SendInfo(WhoAmI, msg + GetRaiseMapRepresentation(stats));

            if (_monitorMode)
            {
                _log.Info("Monitor mode... do not perform trading operations");
                return;
            }
        }

        private string GetDataRepresentation(Dictionary<string, double> data)
        {
            return "[" + string.Join("; ", data.Select(kv => $"{kv.Key} {RaiseRepresentaion(kv.Value)} {NumStr(kv.Value)}")) + "]";
        }

        private string GetRaiseMapRepresentation(Dictionary<string, double> data)
        {
            return "[" + string.Join("; ", data.Select(kv =>
                $"<font color='{(kv.Value > 0 ? "green" : "red")}'>{kv.Key} {RaiseRepresentaion(kv.Value)} {NumStr(kv.Value)}</font>")) + "]";
        }

        private string RaiseRepresentaion(double val)
        {
            return val > 0 ? "▲" : (val < 0 ? "▼" : "-");
        }

        public override void Emulate(DateTime start, DateTime end)
        {
            throw new Exception("Hyper emulation is not supported");
        }

        protected override string WhoAmIValue()
        {
            return base.WhoAmIValue() + (_monitorMode ? " (monitor)" : "");
        }

        protected override void PrintSummary()
        {
            // Do nothing
        }
    }
}
