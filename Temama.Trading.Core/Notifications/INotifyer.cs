namespace Temama.Trading.Core.Notifications
{
    public interface INotifyer
    {
        void SendInfo(string who, string message);

        void SendImportant(string who, string message);

        void SendWarning(string who, string message);

        void SendError(string who, string message);
    }
}
