using System;

namespace Temama.Trading.Core.Utils
{
    public static class UnixTime
    {
        private static DateTime unixBase = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromUnixTime(long unixTimeStamp)
        {
            return unixBase.AddSeconds(unixTimeStamp);
        }

        public static DateTime FromUnixTimeMillis(long unixTimeStamp)
        {
            return unixBase.AddMilliseconds(unixTimeStamp);
        }

        public static long GetUnixTime()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
}
