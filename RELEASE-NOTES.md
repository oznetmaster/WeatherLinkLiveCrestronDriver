# WeatherLinkLiveCrestronDriver v2.0.13

Patch release correcting lifecycle, configuration and recovery defects while preserving the public API and intended driver behavior.

## Fixes

- Reject delayed local and cloud readings after configuration changes or disposal so old responses cannot repopulate caches or restore stale online state.
- Preserve the last local reading when a refresh fails and retain cloud refresh throttling.
- Reject NaN latitude and longitude overrides. Normal operation continues to obtain location from the processor.

## Tests and build process

- 40 offline tests and 16 SDK lifecycle tests. The current implementation passes on Windows in Debug and Release; both processor suites passed twice in the same host process.
- The shared net472 processor test package is available in the solution and appears under **Utility** in Configure. Its standalone Home tile and Windows NUnit runner select the test suites.
- Driver Debug build versions follow the manifest; three-part release tags select the CI release version. Test builds do not increment or deploy the production driver.
- Processor test packages are not published to NuGet. Private deployment settings, live inputs and desktop SDK runtime dependencies are excluded from source and release assets.

## Installation and documentation

The GitHub release includes the production driver package and a separate processor test package. The test package appears under Utility in Configure and is not included in the driver NuGet package. See [CHANGELOG.md](CHANGELOG.md) for release history and [README.md](README.md) for installation and testing.
