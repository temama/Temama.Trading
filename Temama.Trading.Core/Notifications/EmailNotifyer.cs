using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;

namespace Temama.Trading.Core.Notifications
{
    public class EmailNotifyer : INotifyer, IDisposable
    {
        private enum MsgType
        {
            Info,
            Important,
            Warning,
            Error
        }

        private class ToSend
        {
            public string Who { get; set; }
            public MsgType Type { get; set; }
            public string Message { get; set; }

            public ToSend(string who, MsgType msgType, string message)
            {
                Who = who;
                Type = msgType;
                Message = message;
            }
        }

        private Logger.Logger _log;
        private SmtpClient _smtp;
        private MailAddress _from;
        private List<MailAddress> _to;
        private string _toRepr;

        private bool _running;
        private int _interval = 60;
        private DateTime _nextSend;

        private Dictionary<string, List<ToSend>> _toSend = new Dictionary<string, List<ToSend>>();
        private object _lock = new object();

        public EmailNotifyer(XmlNode config, Logger.Logger logger)
        {
            _log = logger;
            var host = config.GetConfigValue("SmtpHost");
            var port = Convert.ToInt32(config.GetConfigValue("SmtpPort"));
            var fromAddr = config.GetConfigValue("FromAddress");
            var pass = config.GetConfigValue("Password");
            _toRepr = config.GetConfigValue("ToAddresses");

            _interval = Convert.ToInt32(config.GetConfigValue("Interval", true, "60"));

            _smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(fromAddr, pass),
                Timeout = 20000
            };

            _from = new MailAddress(fromAddr);
            _to = new List<MailAddress>();
            var tos = _toRepr.Split(';');
            foreach (var to in tos)
            {
                _to.Add(new MailAddress(to));
            }

            Start();
        }
        
        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _nextSend = DateTime.Now.AddSeconds(_interval);
            Task.Run(() =>
            {
                while (_running)
                {
                    if (DateTime.Now < _nextSend)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    CheckAndSend();
                    _nextSend = DateTime.Now.AddSeconds(_interval);
                }
            });
        }

        public void Stop()
        {
            _running = false;
            CheckAndSend();
        }

        private void CheckAndSend()
        {
            lock (_lock)
            {
                if (_toSend.Count == 0)
                    return;
            }

            Dictionary<string, List<ToSend>> toSend;
            lock (_lock)
            {
                toSend = _toSend;
                _toSend = new Dictionary<string, List<ToSend>>();
            }

            try
            {
                using (var mail = new MailMessage())
                {
                    mail.IsBodyHtml = true;
                    mail.From = _from;
                    _to.ForEach(to => mail.To.Add(to));                    
                    mail.Subject = toSend.Count == 1 ? toSend.First().Key : "TemamaTrading notifications";

                    var body = new StringBuilder();
                    foreach (var kvp in toSend)
                    {
                        body.AppendLine($"<h1>{kvp.Key}</h1>");
                        body.AppendLine("<ul>");
                        foreach (var notif in kvp.Value)
                        {
                            body.Append("<ui>");
                            switch (notif.Type)
                            {
                                case MsgType.Important:
                                    body.Append($"<b>{notif.Message}</b>");
                                    break;
                                case MsgType.Warning:
                                    body.Append($"<font color=\"orange\"><b>{notif.Message}</b></font>");
                                    break;
                                case MsgType.Error:
                                    body.Append($"<font color=\"red\"><b>{notif.Message}</b></font>");
                                    break;
                                default:
                                    body.Append(notif.Message);
                                    break;
                            }
                            body.AppendLine("</ui>");
                        }
                        body.AppendLine("</ul>");
                        body.AppendLine("<hr/>");
                    }
                    mail.Body = body.ToString();

                    _smtp.Send(mail);
                    _log.Info($"Email sent to {_toRepr} - {body.ToString()}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to send email notification: {ex.Message}");
            }
        }

        public void SendError(string who, string message)
        {
            lock (_lock)
            {
                if (!_toSend.ContainsKey(who))
                    _toSend[who] = new List<ToSend>();

                _toSend[who].Add(new ToSend(who, MsgType.Error, message));
            }
        }

        public void SendImportant(string who, string message)
        {
            lock (_lock)
            {
                if (!_toSend.ContainsKey(who))
                    _toSend[who] = new List<ToSend>();

                _toSend[who].Add(new ToSend(who, MsgType.Important, message));
            }
        }

        public void SendInfo(string who, string message)
        {
            lock (_lock)
            {
                if (!_toSend.ContainsKey(who))
                    _toSend[who] = new List<ToSend>();

                _toSend[who].Add(new ToSend(who, MsgType.Info, message));
            }
        }

        public void SendWarning(string who, string message)
        {
            lock (_lock)
            {
                if (!_toSend.ContainsKey(who))
                    _toSend[who] = new List<ToSend>();

                _toSend[who].Add(new ToSend(who, MsgType.Warning, message));
            }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
}
