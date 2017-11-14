using System;
using System.IO;

namespace Temama.Trading.Core.Logger
{
    public static class Logger
    {
        private static object _token = new object();
        private static string _fileName = @"Logs\Temama.Trading.log";

        private static ILogHandler _logHandler;

        public static void Init(string executableName, ILogHandler logHandler)
        {
            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");
            _fileName = string.Format("Logs\\{0}_{1}.log", executableName, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            _logHandler = logHandler;
        }

        public static void Spam(string message)
        {
            LogMessage(LogSeverity.Spam, message);
        }

        public static void Info(string message)
        {
            LogMessage(LogSeverity.Info, message);
        }

        public static void Warning(string message)
        {
            LogMessage(LogSeverity.Warning, message);
        }

        public static void Error(string message)
        {
            LogMessage(LogSeverity.Error, message);
        }

        public static void Critical(string message)
        {
            LogMessage(LogSeverity.Critical, message);
        }

        public static void LogMessage(LogSeverity severity, string message)
        {
            try
            {
                lock (_token)
                {
                    File.AppendAllText(_fileName,
                                       string.Format("{0}\t{1}\t{2}\r\n", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                                                     GetSeverityRepresentation(severity), message));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to write log in log file {0} due to exception: {1}", _fileName, ex.Message);
                Console.WriteLine("{0}\t{1}\t{2}\r\n", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                                  GetSeverityRepresentation(severity), message);
            }

            if (_logHandler != null)
                _logHandler.LogMessage(severity, message);
        }

        public static string GetSeverityRepresentation(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Spam:
                    return "SPAM";
                case LogSeverity.Info:
                    return "INFO";
                case LogSeverity.Warning:
                    return "WARNING";
                case LogSeverity.Error:
                    return "ERROR";
                case LogSeverity.Critical:
                    return "CRITICAL";
                default:
                    return "UNKNOWN";
            }
        }

        public static string GetSeverityRepresentationFixedLen(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Spam:
                    return "SPAM    ";
                case LogSeverity.Info:
                    return "INFO    ";
                case LogSeverity.Warning:
                    return "WARNING ";
                case LogSeverity.Error:
                    return "ERROR   ";
                case LogSeverity.Critical:
                    return "CRITICAL";
                default:
                    return "UNKNOWN ";
            }
        }
    }
}
