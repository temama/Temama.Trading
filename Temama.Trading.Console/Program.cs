using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;
using Temama.Trading.Exchanges;

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
                        case LogSeverity.ImportantInfo:
                            System.Console.ForegroundColor = ConsoleColor.White;
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
        private static Dictionary<string, string> LoadArguments(string[] args)
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
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            var configFile = "TradingConfig.xml";
            if (args.Length > 0)
                configFile = args[0];


            Logger.CleanupLogsDir(7);
            var logHandler = new LoggerConsoleEcho();
            Globals.Logger.Init("Temama.Trading.Console", logHandler);
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            ExchangesHelper.RegisterExchanges();
            AlgoHelper.InitAlgos();

            var config = new XmlDocument();
            config.Load(configFile);
            ConfigHelper.InitNotifyersFromConfig(config, Globals.Logger);
            var algos = ConfigHelper.GetAlgosFromConfig(config, logHandler);
            var caption = new StringBuilder();
            var processes = new List<Task>();

            foreach (var algo in algos.Where(a => a.AutoStart))
            {
                caption.Append(algo.WhoAmI + "; ");
                processes.Add(algo.Start());
            }

            //WebServer reportServer = null;
            //try
            //{
            //    reportServer = new WebServer(SendReport, 8877, "/TemamaTrading/Report/");
            //    reportServer.Run();
            //}
            //catch (Exception ex)
            //{
            //    globalLog.Critical("Failed to start reporting server: " + ex.Message);
            //}

            System.Console.Title = "Trading.Console: " + caption;

            Task.WaitAll(processes.ToArray());

            //if (reportServer != null)
            //    reportServer.Stop();
        }

        private static string SendReport(HttpListenerRequest request)
        {
            var resp = new StringBuilder();
            resp.Append("<HTML><BODY><h1>Temama Trading</h1><br>");
            //resp.Append(HtmlReportHelper.ReportRunningBots(new List<TradingBot>() { _algo }));
            resp.Append("</BODY></HTML>");
            return resp.ToString();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Globals.Logger.Critical("Unhandled exception: " + e.ExceptionObject.ToString());
            NotificationManager.SendError("Console", "Unhandled exception: " + e.ExceptionObject.ToString());
        }
    }
}
