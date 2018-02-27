using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Temama.Trading.Core.Common;

namespace Temama.Trading.Core.UnitTests.Common
{
    [TestClass]
    public class SignalTests
    {
        private const string _sEngulfing = "<Signal name=\"engulfing\">{[c2.c]*(1+[c2.b])}a[[|DT(3)|]];r[[|&lt;-0.0005|]];g[[|([c.l]&lt;[c1.l]) and ([c.c]&gt;[c1.o]) and ([c.b]&gt;0.005)|]]</Signal>";

        [TestMethod]
        public void SignalParseTest()
        {
            var xml = new XmlDocument();
            xml.LoadXml(_sEngulfing);
            var s = Signal.Parse(xml.FirstChild);
            Assert.AreEqual(s.SignalName, "engulfing");
            Assert.AreEqual(s.CandlesCount, 3);
            Assert.AreEqual(s.Candles[1].Type, Signal.SignalCandle.SignalCandleType.Red);
            Assert.AreEqual(s.Candles[1].BodyExpr, "[c1.b]<-0.0005");
        }

        [TestMethod]
        public void SignalVerifyTest()
        {
            var xml = new XmlDocument();
            xml.LoadXml(_sEngulfing);
            var s = Signal.Parse(xml.FirstChild);
            var input = new List<Candlestick>() {
                new Candlestick(){Open=308.50132758, High=309.15016772, Low=308.50002753, Close=308.50002753, Volume=17.013438},
                new Candlestick(){Open=308.80828818, High=308.80828818, Low=308.50002753, Close=308.50002754, Volume=9.525559},
                new Candlestick(){Open=308.50002754, High=308.99553914, Low=308,          Close=308.00000002, Volume=36.779032},
                new Candlestick(){Open=308.00000003, High=308.84627473, Low=308.00000003, Close=308.00010004, Volume=176.645203},

                new Candlestick(){Open=308.69453888, High=308.69453888, Low=307,          Close=307,          Volume=254.061422},
                new Candlestick(){Open=307,          High=307.94999417, Low=305.25095999, Close=305.25095999, Volume=223.730017},
                new Candlestick(){Open=305.99998297, High=306.85037309, Low=303.01,       Close=303.0101,     Volume=223.531439},
                new Candlestick(){Open=303.0102,     High=306.54999394, Low=303.000555,   Close=306.54999394, Volume=16.285604}
            };


            /*
            [O:308.50132758 H:309.15016772 L:308.50002753 C:308.50002753 V:17.013438]
            [O:308.80828818 H:308.80828818 L:308.50002753 C:308.50002754 V:9.525559]
            [O:308.50002754 H:308.99553914 L:308 C:308.00000002 V:36.779032]
            [O:308.00000003 H:308.84627473 L:308.00000003 C:308.00010004 V:176.645203]
            [O:308.69453888 H:308.69453888 L:307 C:307 V:254.061422]
            [O:307 H:307.94999417 L:305.25095999 C:305.25095999 V:223.730017]
            [O:305.99998297 H:306.85037309 L:303.01 C:303.0101 V:223.531439]
            [O:303.0102 H:306.54999394 L:303.000555 C:306.54999394 V:16.285604]

             */

            Assert.AreEqual(input[5].MidBody < input[4].MidBody, true);
            Assert.AreEqual(input[4].MidBody < input[3].MidBody, true);
            Assert.AreEqual(input[3].MidBody < input[2].MidBody, true);

            Assert.AreEqual(s.Verify(input), true);
        }
    }
}
