# WeatherLinkLiveCrestronDriver 2.1.0

This minor update restores local station readings automatically after an established connection fails. It uses WeatherLinkLiveLibrary 2.1.0 and retains SimpleWeatherClient 2.0.0. Driver identity, configuration fields, existing commands and UI remain unchanged.

- Keep one local client and let it retry after 10, 20, 30, 40, 50, 60 and 120 seconds, then 300 seconds repeatedly. A successful response resets the sequence and immediately restores local readings.
- Add programmable **WeatherLink Disconnected** (`weatherLinkDisconnected`) and **WeatherLink Reconnected** (`weatherLinkReconnected`) events, once per transition. They describe the local station; working cloud fallback can keep the overall driver online during a local outage. Initial connection does not raise Reconnected.
- Prevent late cloud responses or callbacks from retired clients from replacing newly recovered readings. Preserve the selected units when reading the retained client.

The candidate passed 94 checks on a Crestron processor: 90 unit/lifecycle checks, three real-station reading checks and a physical station-network recovery test. The cable test observed 10-, 20- and 30-second delays and one event per transition. Longer backoff delays are covered by offline tests. This does not represent a new endurance run or Crestron acceptance.

The release includes processor test package **1.2.1**, built with released **CrestronHomeNUnit 2.0.0 / NUnit 5.0.0**. Its manual station network-recovery suite is separate from unattended tests. Repository desktop tests use NUnit3TestAdapter 6.3.0.

This version is available from GitHub and NuGet. Crestron's catalog remains **2.0.19**; submission of this update is deferred.

Support: [contact form](https://oznetmaster.github.io/support/). [Source and documentation](https://github.com/oznetmaster/WeatherLinkLiveCrestronDriver).