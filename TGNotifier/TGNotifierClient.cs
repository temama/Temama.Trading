using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using Temama.Trading.Core.Notifications;
using TLSharp.Core;
using TLSharp.Core.Utils;

namespace TGNotifier
{
    public class TGNotifierClient : INotificator
    {
        private static string _confFile = "TGNotifier.xml";
        private static int _timeout = 10000;

        private TLUser _user;
        private TelegramClient _client;
        //private

        public TGNotifierClient(string seesionId)
        {
            var config = new XmlDocument();
            config.Load(_confFile);

            var node = config.SelectSingleNode("//TGCredentials/AppId");
            var apiId = Convert.ToInt32(node.InnerText);
            node = config.SelectSingleNode("//TGCredentials/ApiHash");
            var apiHash = node.InnerText;
            node = config.SelectSingleNode("//TGCredentials/PhoneNumber");
            var phoneNumber = node.InnerText;

            var session = new FileSessionStore();
            var client = new TelegramClient(apiId, apiHash, session, seesionId);
            var t = client.ConnectAsync();
            if (!t.Wait(_timeout) || t.Result == false)
                throw new Exception("Connection to Telegram server failed");

            if (!client.IsUserAuthorized())
            {
                var hashTask = client.SendCodeRequestAsync(phoneNumber);
                if (!hashTask.Wait(_timeout))
                    throw new Exception("Telegram Notifier - failed to send Code to client");

                var code = Microsoft.VisualBasic.Interaction.InputBox("Please enter code you received in Telegram");

                var authTask = client.MakeAuthAsync(phoneNumber, hashTask.Result, code);
                if (!authTask.Wait(_timeout))
                    throw new Exception("Telegram Notifier - failed to authorize client");
            }

            var getContacts = client.GetContactsAsync();
            if (!getContacts.Wait(_timeout))
                throw new Exception("Failed to get Telegram contact");

            //find recipient in contacts
            var user = getContacts.Result.Users
                .Where(x => x.GetType() == typeof(TLUser))
                .Cast<TLUser>()
                .FirstOrDefault(x => x.Phone == phoneNumber.Trim('+'));

            _user = user ?? throw new Exception("Failed to find Telegram contact " + phoneNumber);
            _client = client;
        }

        public void SendError(string who, string message)
        {
            SendMessage(string.Format("{0}:\nERR: {1}", who, message));
        }

        public void SendImportant(string who, string message)
        {
            SendMessage(string.Format("{0}:\n!! {1}", who, message));
        }

        public void SendInfo(string who, string message)
        {
            SendMessage(string.Format("{0}:\n{1}", who, message));
        }

        private async void SendMessage(string message)
        {
            await _client.SendMessageAsync(new TLInputPeerUser() { UserId = _user.Id }, message);
        }
    }
}
