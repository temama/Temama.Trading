using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temama.Trading.Core.Exchange
{
    public class Funds
    {
        public Dictionary<string, double> Values = new Dictionary<string, double>();

        public virtual string ExchangeName()
        {
            return "Exchange";
        }

        public override string ToString()
        {
            var res = "[" + string.Join("; ", Values.Select(f => f.Key.ToUpper() + "=" + f.Value)) + "]";
            return string.Format("{0} Funds: {1}", ExchangeName(), res);
        }
    }
}
