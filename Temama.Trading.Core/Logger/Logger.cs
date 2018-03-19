using System;
using System.IO;

namespace Temama.Trading.Core.Logger
{
    public class Logger
    {
        public static LogSeverity GlobalLogLevel = LogSeverity.Spam;
        public static bool WriteToFile = true;
        private object _token = new object();
        private string _fileName = "Temama.Trading.log";
        private LogSeverity _logLevel = LogSeverity.Spam;

        private ILogHandler _logHandler;

        public Logger()
        {
            _fileName = Path.Combine("Logs", "Temama.Trading.log");
        }

        public Logger(string ownerName, ILogHandler logHandler, LogSeverity logLevel = LogSeverity.Spam)
        {
            Init(ownerName, logHandler, logLevel);
        }

        public void Init(string ownerName, ILogHandler logHandler, LogSeverity logLevel = LogSeverity.Spam)
        {
            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            _fileName = Path.Combine("Logs", string.Format("{0}_{1}.log", ownerName, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            _logHandler = logHandler;
            _logLevel = logLevel;
        }

        public void Spam(string message)
        {
            LogMessage(LogSeverity.Spam, message);
        }

        public void Info(string message)
        {
            LogMessage(LogSeverity.Info, message);
        }

        public void Important(string message)
        {
            LogMessage(LogSeverity.ImportantInfo, message);
        }

        public void Warning(string message)
        {
            LogMessage(LogSeverity.Warning, message);
        }

        public void Error(string message)
        {
            LogMessage(LogSeverity.Error, message);
        }

        public void Critical(string message)
        {
            LogMessage(LogSeverity.Critical, message);
        }

        public void LogMessage(LogSeverity severity, string message)
        {
            if (severity < GlobalLogLevel)
                return;

            if (severity < _logLevel)
                return;

            if (WriteToFile)
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
                case LogSeverity.ImportantInfo:
                    return "IMPORTANT";
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
                case LogSeverity.ImportantInfo:
                    return "!!INFO!!";
                default:
                    return "UNKNOWN ";
            }
        }

        /// <summary>
        /// Delete log files older than <para>days</para> days
        /// </summary>
        /// <param name="days">Days to keep logs</param>
        public static void CleanupLogsDir(int days)
        {
            if (!Directory.Exists("Logs"))
                return;

            string[] files = Directory.GetFiles("Logs");

            foreach (string file in files)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTime < DateTime.Now.AddDays(-1 * days))
                    fi.Delete();
            }
        }
    }
}
