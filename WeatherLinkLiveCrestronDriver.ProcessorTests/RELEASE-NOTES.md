# WeatherLinkLiveCrestronDriver Tests

## 1.1.4

- Build with SimpleWeatherClient 2.0.0 and WeatherLinkLiveLibrary 2.0.0, using typed System.Text.Json responses and optional library logging.
- Cover failed-cloud-attempt throttling and continued local readings during cloud backoff.
- Add a separately selectable live-cloud suite. Supply private CloudTestSettings.json with the OpenWeather key and coordinates; requests count toward the account's usage.
- This processor test package is published on GitHub only, not NuGet. It does not update the installed production driver.

## 1.1.3

- Add regression tests for offline status, retained readings, empty cloud responses, source recovery, forecast-only failures and late failed requests after configuration is cleared.
- Space read-only live station requests at the documented ten-second interval and include the underlying station-read error in failures. Tests do not automatically retry failed reads.
- Include the driver 2.0.17 availability fix in the test assembly. The production driver is independently installed and released.
- This processor test package is published on GitHub only, not NuGet.

## 1.1.2

- Add regression coverage for local and cloud pressure units, cloud wind and rainfall units, and the values displayed by the driver.
- Build with CrestronHomeNUnit test-host SDK 1.12.1. Validate discovered test identities against the desktop suite instead of maintaining a separate expected-count constant.
- Validate the unit, lifecycle and optional read-only live suites on a processor. Live tests require the station address in private inputs and do not change station settings.
- Include temporary-instance and stored-package cleanup in automated test workflows. Home may retain a cached catalogue entry until its next planned reboot.
- This test package is published on GitHub only; it is not a NuGet package.

## 1.1.1

- Rebuild with CrestronHomeNUnit 1.2.1. Test execution now participates in the shared processor reservation used by the runner, Test Explorer, CLI and hardware CI.
- The net472 package contains 59 discovered cases, with 56 in automatic suites. Live suites remain optional and require private inputs where documented.
- Use the standalone Utility tile, Windows runner, or the solution's Test Explorer workflow project. Private workflow plans can remove the temporary instance after testing.
- This is an independent processor-test package release on GitHub; it does not publish or update a driver/library NuGet package.

- Add three optional live weather station tests for measured readings, repeated refresh and unit selection. The package contains 59 tests in total.
- Supply the station address through private runner inputs. Tests read station data without changing station settings.

## 1.1.0 — 2026-09-14

- 40 offline tests and 16 SDK lifecycle tests, shared between desktop validation and the net472 processor package.
- Cover local readings, failure cache retention, cloud refresh throttling and delayed local/cloud responses after configuration changes or disposal. Reject stale responses before they can restore cached weather or online state.
- Install the standalone test package from Configure’s **Utility** category. Select suites using its Home tile or the Windows NUnit runner.
- Processor test packages are GitHub release assets and are not published to NuGet. Private test inputs and deployment settings are excluded.
