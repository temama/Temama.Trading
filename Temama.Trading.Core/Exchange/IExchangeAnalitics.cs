using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public interface IExchangeAnalitics
    {
        List<Tick> GetRecentPrices(string baseCur, string fundCur, DateTime fromDate, int maxResultCount = 100);
    }
}
