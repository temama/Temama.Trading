using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;

namespace Temama.Trading.Core.Utils
{
    public static class ConfigHelper
    {
        public static string GetConfigValue(this XmlNode config, string configName, bool optional = false, string defaultValue = "")
        {
            var node = config.SelectSingleNode(configName);
            if (node != null)
                return node.InnerText;

            if (optional)
                return defaultValue;

            throw new Exception($"Config {configName} is not found");
        }

        public static List<INotifyer> InitNotifyersFromConfig(XmlDocument config, Logger.Logger logger)
        {
            var res = new List<INotifyer>();

            foreach (XmlNode notif in config.SelectNodes("//TemamaTradingConfig/Notifyer"))
            {
                var notifyer = NotificationManager.Create(notif, logger);
                NotificationManager.Add(notifyer);
                res.Add(notifyer);
            }

            return res;
        }

        public static List<IAlgo> GetAlgosFromConfig(XmlDocument config, ILogHandler logHandler)
        {
            var res = new List<IAlgo>();
            foreach (XmlNode botConf in config.SelectNodes("//TemamaTradingConfig/Bot"))
            {
                var nameAttr = botConf.Attributes["name"];
                if (nameAttr != null)
                    res.Add(Globals.CreateBotByName(nameAttr.Value, botConf, logHandler));
                else
                    throw new Exception("No Bot name at config: " + botConf.OuterXml);
            }

            foreach (XmlNode monitorConf in config.SelectNodes("//TemamaTradingConfig/Monitor"))
            {
                var nameAttr = monitorConf.Attributes["name"];
                if (nameAttr != null)
                    res.Add(Globals.CreateMonitorByName(nameAttr.Value, monitorConf, logHandler));
                else
                    throw new Exception("No Monitor name at config: " + monitorConf.OuterXml);
            }

            return res;
        }
    }
}
