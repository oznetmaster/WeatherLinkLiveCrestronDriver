# Changelog

## WeatherLinkLiveCrestronDriver.ProcessorTests v1.1.1 - 2026-09-15

Published processor test package on GitHub. This is a test-package release only; no driver or library NuGet package is published. See the matching package release notes for changes and validation.

## 2026-09-15 - Test and development tooling (no driver release)

- Add the published Test Explorer workflow adapter, offline discovery CI and independent GitHub processor-test releases. Private workflow plans control optional live tests, actual-driver updates and temporary-instance cleanup.


- Add three optional live driver tests for measured station readings, repeated refresh and unit selection. The processor package now contains 59 tests. Private station settings are supplied as test inputs and are never packaged.

- Clarify that automatic forecast coordinates come from the native processor location, not Home's separate setting. Document the native console command, SDK reboot requirement, and paired driver override alternative. Documentation only; no driver behavior change.

## 2.0.13 — 2026-09-14

[Driver release notes](RELEASE-NOTES.md). Test-only changes do not require a driver release.

- Cover local readings, failure cache retention, cloud refresh throttling and delayed local/cloud responses after configuration changes or disposal. Reject stale responses before they can restore cached weather or online state.

- Normalize the working manifest from `2.0.011.0005` to `2.0.012.0005`; this aligns the development version family with the latest existing three-part release. No historical tags or packages are changed.

- Standardize driver versioning: Debug project/package metadata follows the manifest including its build increment; local Release builds preserve it; three-part release tags select the exact CI release without another patch increment. Verify source and built package versions before publication.

- Expand driver coverage to 40 offline tests and 16 SDK entity/lifecycle tests, with a desktop SDK harness and the same lifecycle fixtures in the net472 processor package.
- Reject NaN latitude and longitude overrides. Add an internal location provider for deterministic desktop entity testing; the production constructor still reads the processor location.

## 2.0.12 - 2026-09-13

- Add 40 NUnit driver unit tests and a processor lifecycle suite in the existing solution.
- Add a standalone Utility processor test package with private Debug deployment settings.
- Update WeatherLinkLiveLibrary to 1.0.3, SimpleWeatherClient to 1.0.3 and the SIMPL# SDK library to 2.22.15.

### Validation

- 40 offline NUnit tests pass on Windows; the actual packaged unit suite also passes twice in one process.
- The processor lifecycle test is available for hardware validation and is skipped on Windows.
- The production driver package and its NuGet wrapper retain the existing public driver behavior; dependency updates supply the library fixes.