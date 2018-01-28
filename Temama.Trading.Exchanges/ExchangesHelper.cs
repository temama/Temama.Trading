using System;
using System.Xml;
using Temama.Trading.Core.Exchange;
using Temama.Trading.Core.Logger;
using Temama.Trading.Core.Utils;
using Temama.Trading.Exchanges.Cex;
using Temama.Trading.Exchanges.Emu;
using Temama.Trading.Exchanges.Exmo;
using Temama.Trading.Exchanges.Kuna;

namespace Temama.Trading.Exchanges
{
    public class ExchangesHelper
    {
        public static void RegisterExchanges()
        {
            Globals.CreateApiByName = CreateApiByName; 
        }
        
        public static ExchangeApi CreateApiByName(string apiName, XmlNode config, Logger logger)
        {
            switch (apiName.ToLower())
            {
                case "cex":
                    return new CexApi(config, logger);
                case "kuna":
                    return new KunaApi(config, logger);
                case "exmo":
                    return new ExmoApi(config, logger);
                case "emulator":
                    return new EmuApi(config, logger);
            }            

            throw new Exception($"No Exchange API with name={apiName} found");
        }
    }
}
