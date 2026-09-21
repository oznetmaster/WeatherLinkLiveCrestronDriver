# Changelog

This changelog records shipped features, fixes, compatibility and runtime dependency changes. See [development and validation history](DEVELOPMENT-HISTORY.md) for tests, CI, build tooling and work not yet released.

## 2.0.17 - 2026-09-21

- Correct offline status when current-weather retrieval fails, retaining the last available readings and showing the refresh failure.
- Restore online status after current-weather recovery, and keep fresh local conditions online when only forecast retrieval fails.

[Release notes](release-notes/v2.0.17.md). Driver identity, configuration, commands and runtime dependencies are unchanged.

## 2.0.16 - 2026-09-21

- Correct local metric pressure display (mmHg to hPa) and cloud pressure display in UK/Imperial mode (hPa to inHg).
- Correct cloud fallback wind units for Metric/UK and rainfall units for Imperial.
- Preserve driver initialization with the named package assembly and include product help and licence notices.

[Release notes](release-notes/v2.0.16.md). Driver identity, configuration and public commands are unchanged.

## 2.0.14 - 2026-09-15

- Correct the driver tile to appear on the Home screen only, rather than both Home and room screens. Room assignment, configuration and public commands are unchanged.

## 2.0.13 — 2026-09-14

[Driver release notes](RELEASE-NOTES.md). Test-only changes do not require a driver release.

- Reject stale local and cloud responses after configuration changes or disposal, before they can restore cached weather or online state.

- Reject NaN latitude and longitude overrides. The production constructor continues to read the processor location.

## 2.0.12 - 2026-09-13

- Update WeatherLinkLiveLibrary to 1.0.3, SimpleWeatherClient to 1.0.3 and the SIMPL# SDK library to 2.22.15.

- The production driver package and its NuGet wrapper retain the existing public driver behavior; dependency updates supply the library fixes.
