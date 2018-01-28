using System;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Algo
{
    public class AlgoHelper
    {
        public static void InitAlgos()
        {
            Globals.CreateBotByName = CreateBotByName;
            Globals.CreateMonitorByName = CreateMonitorByName;
        }

        public static TradingBot CreateBotByName(string botName, XmlNode config, ILogHandler logHandler)
        {
            switch (botName.ToLower())
            {
                case "ranger":
                    return new Ranger(config, logHandler);
                case "rangerpro":
                    return new RangerPro(config, logHandler);
                case "shaper":
                    return new Shaper(config, logHandler);
                case "sheriff":
                    return new Sheriff(config, logHandler);
            }

            throw new Exception($"No Bot with name={botName} found");
        }

        public static MarketMonitor CreateMonitorByName(string monitorName, XmlNode config, ILogHandler logHandler)
        {
            switch (monitorName.ToLower())
            {

            }

            throw new Exception($"No Monitor with name={monitorName} found");
        }
    }
}
