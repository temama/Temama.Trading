using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Notifications
{
    public class NotificationManager
    {
        private static INotificator _notificator;

        public static void Init(INotificator notificator)
        {
            _notificator = notificator;
        }

        public static void SendError(string who, string message)
        {
            if (_notificator == null)
                return;
            
            _notificator.SendError(who, message);
        }

        public static void SendInfo(string who, string message)
        {
            if (_notificator == null)
                return;
            
            _notificator.SendInfo(who, message);
        }

        public static void SendImportant(string who, string message)
        {
            if (_notificator == null)
                return;
            
            _notificator.SendImportant(who, message);
        }
    }
}
