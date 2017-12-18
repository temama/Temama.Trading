using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Temama.Trading.Algo;
using Temama.Trading.Core.Algo;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Notifications;
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

        static void Main(string[] args)
        {
            Logger.Init("Temama.Trading.Emulator", new LoggerConsoleEcho());
            System.AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            var config = new XmlDocument();
            config.Load("EmulatorConfig_CexEthUsd.xml");

            var api = new EmuApi();
            api.Init(config);

            var algo = new RangerPro();
            algo.Init(api, config);

            algo.Emulate(new DateTime(2017, 5, 1), new DateTime(2017, 6, 1));

            Logger.Info("Emulation done...");
            Console.ReadKey();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Critical("Unhandled exception: " + e.ExceptionObject.ToString());
            NotificationManager.SendError("Console", "Unhandled exception: " + e.ExceptionObject.ToString());
        }
    }
}
