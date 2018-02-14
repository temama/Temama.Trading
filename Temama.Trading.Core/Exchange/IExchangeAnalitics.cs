using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public interface IExchangeAnalitics
    {
        void SetHistoricalTradesPersistInterval(string baseCur, string fundCur, TimeSpan duration);

        bool HasHistoricalDataStartingFrom(string baseCur, string fundCur, DateTime dateTime, bool fetchLatest = false);

        List<Trade> GetRecentTrades(string baseCur, string fundCur, DateTime fromDate);
    }
}
