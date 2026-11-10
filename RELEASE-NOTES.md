# WeatherLinkLiveCrestronDriver 2.1.1

This patch corrects the dependency and lifecycle issues described below.

- Use WeatherLinkLiveLibrary 2.1.1 and SimpleWeatherClient 2.0.1.
- Compile routine informational fallback messages out of Release builds while retaining warnings and errors.
- Preserve station configuration, units and existing recovery behavior.

## Installation

Use the driver package from [GitHub](https://github.com/oznetmaster/WeatherLinkLiveCrestronDriver/releases/tag/v2.1.1) or the matching NuGet distribution. Import the package, then update the existing driver in Home Setup or Configure Pro. Importing alone does not update the running instance. Existing pairing and configuration formats are retained; removal and re-add is not a routine update step.

## Validation

Passed 139 workstation Debug and 139 Release test executions, plus 91 native net472/Mono processor tests. Processor validation used fake transports and synthetic credentials; no new physical-device acceptance or endurance run is claimed. Existing processor configuration and all pairing files were preserved. See [development history](DEVELOPMENT-HISTORY.md) for provenance and limits.

This GitHub/NuGet release does not replace any submitted Crestron package. Submission-specific PDFs and correspondence are excluded from public artifacts.
