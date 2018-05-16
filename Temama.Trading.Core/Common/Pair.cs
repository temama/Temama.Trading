using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Temama.Trading.Core.Common
{
    public class Pair
    {
        public string Base { get; set; }
        public string Fund { get; set; }

        public static Pair Parse(XmlNode node)
        {
            if (node.Attributes == null)
                throw new Exception("Pair XML node should contain 'base' & 'fund' attributes");

            return new Pair
            {
                Base = node.Attributes["base"].Value,
                Fund = node.Attributes["fund"].Value
            };
        }

        public override string ToString()
        {
            return $"{Base}/{Fund}";
        }
    }
}
