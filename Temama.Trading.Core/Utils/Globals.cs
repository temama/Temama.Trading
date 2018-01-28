using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;

namespace Temama.Trading.Core.Utils
{
    public class Globals
    {
        public delegate ExchangeApi ApiByNameCreator(string apiName, XmlNode config, Logger.Logger logger);
        public delegate TradingBot BotByNameCreator(string botName, XmlNode config, ILogHandler logHandler);
        public delegate MarketMonitor MonitorByNameCreator(string monitorName, XmlNode config, ILogHandler logHandler);

        public static ApiByNameCreator CreateApiByName;
        public static BotByNameCreator CreateBotByName;
        public static MonitorByNameCreator CreateMonitorByName;

        public static Logger.Logger Logger = new Logger.Logger();
    }
}
