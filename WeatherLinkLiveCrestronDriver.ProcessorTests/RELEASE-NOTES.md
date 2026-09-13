# WeatherLinkLiveCrestronDriver Tests

## 1.1.0 — 2026-09-14

- 40 offline tests and 16 SDK lifecycle tests, shared between desktop validation and the net472 processor package.
- Cover local readings, failure cache retention, cloud refresh throttling and delayed local/cloud responses after configuration changes or disposal. Reject stale responses before they can restore cached weather or online state.
- Install the standalone test package from Configure’s **Utility** category. Select suites using its Home tile or the Windows NUnit runner.
- Processor test packages are GitHub release assets and are not published to NuGet. Private test inputs and deployment settings are excluded.
