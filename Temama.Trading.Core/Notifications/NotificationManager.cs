using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Logger;

namespace Temama.Trading.Core.Notifications
{
    public class NotificationManager
    {
        private static List<INotifyer> notifyers = new List<INotifyer>();

        public static INotifyer Create(XmlNode config, Logger.Logger logHandler)
        {
            var nameAttr = config.Attributes["name"];
            if (nameAttr == null)
                throw new Exception("No Notifyer name at config: " + config.OuterXml);

            switch (nameAttr.Value.ToLower())
            {
                case "email":
                    return new EmailNotifyer(config, logHandler);
                default:
                    throw new Exception("Unknown Notifyer: " + nameAttr.Value);
            }
        }

        public static void Add(INotifyer notifyer)
        {
            notifyers.Add(notifyer);
        }

        public static void SendError(string who, string message)
        {
            foreach (var n in notifyers)
            {
                n.SendError(who, message);
            }
        }

        public static void SendInfo(string who, string message)
        {
            foreach (var n in notifyers)
            {
                n.SendInfo(who, message);
            }
        }

        public static void SendImportant(string who, string message)
        {
            foreach (var n in notifyers)
            {
                n.SendImportant(who, message);
            }
        }

        public static void SendWarning(string who, string message)
        {
            foreach (var n in notifyers)
            {
                n.SendWarning(who, message);
            }
        }
    }
}
