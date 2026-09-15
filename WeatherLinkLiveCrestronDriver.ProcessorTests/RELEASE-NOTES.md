# WeatherLinkLiveCrestronDriver Tests

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