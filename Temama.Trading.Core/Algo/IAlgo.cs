using System;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Algo
{
    public interface IAlgo
    {
        bool AutoStart { get; }

        string WhoAmI { get; }

        string Name();

        Task Start();

        void Stop();

        void Emulate(DateTime start, DateTime end);
    }
}
