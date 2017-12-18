# Temama.Trading
Play arround with crypto currency exchange bots
## Quick start:
* Compile Temama.Trading.Console project or download latest [release](https://github.com/temama/Temama.Trading/releases)
* Create config file
  * Config file name should have follow format: ```<Algorithm>_<ExchangeName>_<BaseCurrency><FiatCurrency><Suffix>.xml```
  * You can modify one of existing ```<Algorithm>SampleConfig.xml```. Populate config file with your Public/Secret keys (and UserID for Cex.io)
  * Set your parameters
  * Suffixes in config files are used to run same algo on the same exchange but for different market "moods" (i.e. if you expect currency will grow fast). Actually typically it's not used
* Run Console with parameters:
```bash
Algo=<Algorithm> Exchange=<ExchangeName> base=<BaseCurrency> fund=<FiatCurrency> configsuffix=<ConfigFileSuffix>
#Example:
Temama.Trading.Console.exe Algo=RangerPro Exchange=Cex base=eth fund=usd configsuffix=_normal
```
* Profit
