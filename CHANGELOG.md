# Changelog

## 2.0.12 - 2026-09-13

- Add 40 NUnit driver unit tests and a processor lifecycle suite in the existing solution.
- Add a standalone Utility processor test package with private Debug deployment settings.
- Update WeatherLinkLiveLibrary to 1.0.3, SimpleWeatherClient to 1.0.3 and the SIMPL# SDK library to 2.22.15.

### Validation

- 40 offline NUnit tests pass on Windows; the actual packaged unit suite also passes twice in one process.
- The processor lifecycle test is available for hardware validation and is skipped on Windows.
- The production driver package and its NuGet wrapper retain the existing public driver behavior; dependency updates supply the library fixes.
