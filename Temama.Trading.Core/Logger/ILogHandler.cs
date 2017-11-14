namespace Temama.Trading.Core.Logger
{
    public interface ILogHandler
    {
        void LogMessage(LogSeverity severity, string message);
    }
}
