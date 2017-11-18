using System;
using System.Collections.Generic;
using System.Xml;
using Temama.Trading.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Exchanges.Cex;
using Temama.Trading.Exchanges.Kuna;
using TGNotifier;

namespace Temama.Trading.Console
{
    public class Program
    {
        public class LoggerConsoleEcho : ILogHandler
        {
            private static object _token = new object();

            public void LogMessage(LogSeverity severity, string message)
            {
                // don't spam at console. It will be written in log file
                if (severity == LogSeverity.Spam)
                    return;

                lock (_token)
                {
                    switch (severity)
                    {
                        case LogSeverity.Critical:
                            System.Console.ForegroundColor = ConsoleColor.Red;
                            break;
                        case LogSeverity.Error:
                            System.Console.ForegroundColor = ConsoleColor.DarkRed;
                            break;
                        case LogSeverity.Warning:
                            System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                            break;
                        default:
                            System.Console.ForegroundColor = ConsoleColor.Gray;
                            break;
                    }

                    System.Console.WriteLine(string.Format("{0}:{1}: {2}",
                        DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                        Logger.GetSeverityRepresentationFixedLen(severity), message));
                }
            }
        }

        /// <summary>
        /// Format Parameter=Value Parameter2=Value2
        /// </summary>
        /// <param name="args"></param>
        private static Dictionary<string,string> LoadArguments(string[] args)
        {
            var res = new Dictionary<string, string>();

            foreach (var arg in args)
            {
                var parts = arg.Split('=');
                res[parts[0].ToLower()] = parts[1];
            }

            return res;
        }

        public static void Main(string[] args)
        {
            var sParams = LoadArguments(args);
            var file = string.Format("{0}_{1}_{2}{3}{4}", sParams["algo"], sParams["exchange"], sParams["base"], sParams["fund"],
                sParams.ContainsKey("configsuffix") ? sParams["configsuffix"] : string.Empty);

            Logger.Init(file, new LoggerConsoleEcho());
            System.AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            var config = new XmlDocument();
            config.Load(file + ".xml");

            TGNotifierClient tgNotificator;
            try
            {
                tgNotificator = new TGNotifierClient(string.Format("{0}_{1}_{2}{3}", sParams["algo"], sParams["exchange"], sParams["base"], sParams["fund"]));
                NotificationManager.Init(tgNotificator);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create TGNotifier: " + ex.Message);
            }

            IExchangeApi api;
            switch (sParams["exchange"].ToLower())
            {
                case "kuna":
                    api = new KunaApi();
                    break;
                case "cex":
                    api = new CexApi();
                    break;
                default:
                    throw new Exception(string.Format("Unknown Exchange {0}", sParams["exchange"]));
            }
            api.Init(config);

            Algorithm algo;
            switch (sParams["algo"].ToLower())
            {
                case "sheriff":
                    algo = new Sheriff();
                    break;
                case "ranger":
                    algo = new Ranger();
                    break;
                case "rangerpro":
                    algo = new RangerPro();
                    break;
                default:
                    throw new Exception(string.Format("Unknown Algorithm {0}", sParams["algo"]));
            }
            algo.Init(api, config);

            System.Console.Title = string.Format("{0} on {1}. Config: {2}", sParams["algo"], sParams["exchange"], file);

            //(algo as Ranger).Test();
            var t = algo.StartTrading();
            t.Wait();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Critical("Unhandled exception: " + e.ExceptionObject.ToString());
            NotificationManager.SendError("Console", "Unhandled exception: " + e.ExceptionObject.ToString());
        }
    }
}
