using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Algo
{
    public class MarketMonitor : IAlgo
    {
        public bool AutoStart { get; private set; }

        public string WhoAmI { get; }

        public string Name()
        {
            return "MarketMonitor";
        }

        public void Emulate(DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        public Task Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
