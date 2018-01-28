using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Algo;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
using Temama.Trading.Core.Utils;
using Temama.Trading.Exchanges;
using Temama.Trading.Exchanges.Emu;

namespace Temama.Trading.Emulator
{
    class Program
    {
        public class LoggerConsoleEcho : ILogHandler
        {
            private static object _token = new object();

            public void LogMessage(LogSeverity severity, string message)
            {
                // don't spam at console. It will be written in log file
                if (severity < LogSeverity.ImportantInfo)
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

        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            var configFile = "EmulationConfig.xml";
            if (args.Length > 0)
                configFile = args[0];

            Logger.CleanupLogsDir(2);
            var logHandler = new LoggerConsoleEcho();
            Globals.Logger.Init("Temama.Trading.Emulator", logHandler);
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            ExchangesHelper.RegisterExchanges();
            AlgoHelper.InitAlgos();

            var config = new XmlDocument();
            config.Load(configFile);
            var node = config.SelectSingleNode("//TemamaTradingConfig");
            var start = DateTime.Parse(node.GetConfigValue("StartDate"));
            var end = DateTime.Parse(node.GetConfigValue("EndDate"));

            var algos = ConfigHelper.GetAlgosFromConfig(config, logHandler);
            
            var processes = new List<Task>();

            foreach (var algo in algos.Where(a => a.AutoStart))
            {
                Console.Title = "Emulating: " + algo.WhoAmI;
                Globals.Logger.Info("Starting emulation: " + algo.WhoAmI);
                algo.Emulate(start, end);
                Globals.Logger.Info("Emulation is done for: " + algo.WhoAmI);
            }
            
            Task.WaitAll(processes.ToArray());

            Globals.Logger.Info("Emulation completted");
            Console.ReadKey();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Globals.Logger.Critical("Unhandled exception: " + e.ExceptionObject.ToString());
            NotificationManager.SendError("Console", "Unhandled exception: " + e.ExceptionObject.ToString());
        }
    }
}
