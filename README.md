# WeatherLinkLiveCrestronDriver

For shipped changes, see the [changelog](CHANGELOG.md). Test, CI and build history is recorded separately in [development and validation history](DEVELOPMENT-HISTORY.md).


The driver tile appears on the **Home screen only**. It is not displayed on room screens; its room assignment remains available for configuration.

See the [changelog](CHANGELOG.md) for release history and the [release notes](RELEASE-NOTES.md) for the current driver update. Driver releases are made for runtime fixes or dependency changes; adding tests alone does not require a driver release.

A **Crestron Home** extension driver that integrates a local **WeatherLink Live™** device for current conditions and uses **OpenWeather** cloud data for forecast information and fallback current conditions when the local device is unavailable.

Crestron and Crestron Home are trademarks or registered trademarks of Crestron Electronics, Inc. This project is not affiliated with, endorsed by, or sponsored by Crestron Electronics, Inc.

WeatherLink and WeatherLink Live are trademarks of Davis Instruments Corp. This independent project is not affiliated with, endorsed by, or sponsored by Davis Instruments Corp.

[![License: MIT + Commons Clause](https://img.shields.io/badge/License-MIT%20%2B%20Commons%20Clause-blue.svg)](LICENSE)

---

## Driver Architecture

This driver is a **Crestron Home weather-station extension driver** implemented on the **Crestron Home SDK V2 Entity Model**. It derives directly from `ReflectedAttributeDriverEntity` and exposes all configuration items, properties, commands, and extension UI bindings through SDK attributes and the entity model.

The driver uses a dual-source weather model:

- **Local WeatherLink Live™** for current conditions such as temperature, humidity, wind, pressure, and rainfall
- **OpenWeather** via `SimpleWeatherClient` for forecast data and cloud fallback current conditions

The current-conditions path is designed to prefer the local WeatherLink Live device whenever it is available, while cloud data is used for forecast details and as a fallback when the local device cannot be reached.

---

## Features

- Local WeatherLink Live polling for current conditions
- OpenWeather forecast support
- Cloud fallback current conditions when WeatherLink Live is unavailable
- Startup cloud refresh for forecast/chance-of-rain population
- Daily automatic cloud refresh after 00:01 local time
- Forecast page command-driven refresh support from the extension UI
- Metric, Imperial, and UK hybrid unit handling
- Optional location name, latitude, and longitude overrides for cloud weather requests and title display
- Crestron Home extension UI with current conditions page, tile summary, and weekly forecast page
- Weather-condition to Crestron icon mapping for tile display, with local numeric current-condition data preferred over cloud fallback when reliable

---

## Prerequisites

| Requirement | Details |
|---|---|
| Crestron Home processor | Running a firmware version compatible with extension drivers |
| WeatherLink Live device | Optional but recommended for local current conditions |
| OpenWeather API key | Required for forecast data and cloud fallback current conditions. A free OpenWeather account is sufficient for current conditions and the reduced five-day forecast; a One Call subscription is optional. |
| Processor location or coordinate overrides | Native processor latitude/longitude must be configured, or supply both driver overrides |

---

## Installation

The best way to download and install this driver on a Crestron Home system is to use the [Crestron Home Driver Feed Installer](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer) repository and application.

If you prefer to install manually, use the attached `.pkg` asset from the relevant GitHub Release. The automatic GitHub `Source code (zip)` and `Source code (tar.gz)` assets are repository snapshots, not installable Crestron driver packages.

NuGet package availability: this driver is also published as the `CrestronHomeDriver.WeatherLinkLive.WeatherStation` NuGet package. This NuGet package conforms to the **Crestron Home Driver NuGet Publishing Standard v1**. It is a distribution wrapper for the final `.pkg` artifact, includes the required `crestron-driver-package.json` manifest, and is not intended as a direct DLL reference package.

Crestron Home Driver NuGet Publishing Standard v1 is **not** an official Crestron product or specification. It is an open source packaging standard created to facilitate community distribution and discovery of Crestron Home drivers through NuGet.

1. Download the generated `.pkg` asset from the GitHub Release, or build it yourself using the instructions in [Building from Source](#building-from-source).
2. Upload the `.pkg` file to your Crestron Home processor manually (for example via SFTP to `/user/ThirdPartyDrivers/Import`).
3. In the Crestron Home configuration UI, add a new device and select the **WeatherLink Live Weather Station** driver.
4. Configure the driver:

| Field | Description |
|---|---|
| WeatherLink Live Host | Optional. IP address or hostname of the local WeatherLink Live device |
| OpenWeather API Key | Required. Used for forecast data and cloud fallback current conditions. A free OpenWeather account is sufficient for current conditions and the reduced five-day forecast. A One Call subscription is optional; when unavailable, `SimpleWeatherClient` falls back to the free endpoints. No separate `SimpleWeatherClient` key is needed. |
| Location Name Override | Optional. Overrides the title location name shown on the current conditions page |
| Latitude Override | Optional. Leave blank to use the native processor latitude for cloud weather requests |
| Longitude Override | Optional. Leave blank to use the native processor longitude for cloud weather requests |
| Units | `Metric`, `UK`, or `Imperial` |
| Refresh Interval Seconds | Refresh interval for scheduled current-condition updates; forecast/cloud refreshes follow their own startup, manual, and daily refresh rules |

If a WeatherLink Live host is supplied, the driver prefers it for current conditions. If it is unavailable at a given refresh, the driver can fall back to cached/throttled cloud weather data.

The current conditions and weekly forecast page title locations use the following priority order:

1. **Location Name Override**
2. City name returned by the OpenWeather current weather response
3. Reverse-geocoded city name from the configured/effective coordinates
4. Effective latitude/longitude text

Latitude and longitude overrides are optional, but they must both be supplied together. When left blank, the driver reads the native processor location through `CrestronEnvironment.Latitude` and `CrestronEnvironment.Longitude` for cloud weather access.

---

## Checking the Forecast Location

The location configured in Home can differ from the native processor location returned by the SIMPL# SDK. The driver reads the native processor values; a location name override changes the displayed title only, not the forecast coordinates.

In the processor console, use `LOCATION` to read the native coordinates. Use `LOCATION ?` to see the setting syntax; `-LAT:` accepts north-positive latitude and `-LON:` accepts east-positive longitude (west is negative). The Home commands `SETLATITUDE` and `SETLONGITUDE` change Home's own settings and are not substitutes for the native `LOCATION` command.

The SDK caches its location values and documents that changing them requires a processor reboot. Reloading an individual V2 driver may still leave it reading the old values. After changing the native location and rebooting, check the driver's displayed coordinates and request a fresh forecast. Alternatively, configure both driver coordinate overrides to avoid relying on the processor location.

---

## Current and Forecast Refresh Behavior

- **Startup:** current conditions refresh immediately, and cloud forecast data is also initialized so forecast-related fields are populated
- **Scheduled updates:** current conditions refresh on the configured interval
- **Forecast button:** the forecast page can request a cloud refresh when needed
- **Cloud throttling:** normal cloud requests are limited to once every 10 minutes, except for the daily post-00:01 refresh trigger

Online status describes the availability of current-weather data. A working cloud fallback can keep the driver online while the local station is unreachable. If current-weather retrieval fails with no usable fallback, the driver goes offline while retaining any last available readings with a failed-update status. A successful current-weather refresh restores online status. A forecast-only failure does not make fresh local current conditions offline.

For tile icon selection, the driver prefers direct local numeric WeatherLink Live data whenever it is reliable:

- local rain rate determines rain versus non-rain conditions
- local rain rate combined with below-freezing temperature determines snow/freezing precipitation
- local sustained wind and gust thresholds determine windy conditions
- cloud weather icon/description is used as fallback only when local numeric data cannot determine the icon confidently

---

## Building from Source

### Dependencies

- [WeatherLinkLiveLibrary](https://www.nuget.org/packages/WeatherLinkLiveLibrary) NuGet package
- [SimpleWeatherClient](https://www.nuget.org/packages/SimpleWeatherClient) NuGet package
- [Crestron.DeviceDrivers.DevKit](https://www.nuget.org/packages/Crestron.DeviceDrivers.DevKit) NuGet package
- [Crestron.SimplSharp.SDK.Library](https://www.nuget.org/packages/Crestron.SimplSharp.SDK.Library) NuGet package
- `.NET Framework 4.7.2`
- [ILRepack](https://github.com/gluck/il-repack) via `ILRepackMerge.ps1`
- `PatchMergedAssembly.ps1` to rewrite merged assemblies for Crestron Home runtime compatibility
- `ManifestUtil.exe` from the complete `Crestron.DeviceDrivers.ManifestUtil` 29.0.10 NuGet tool package to produce the final `.pkg`

### Build

```powershell
dotnet build WeatherLinkLiveCrestronDriver.slnx -c Release
```

The build pipeline:
1. Compiles the driver targeting `net472`
2. Bumps `DriverVersion` and `VersionDate` in `WeatherlinkLiveCrestronDriver.json`
3. ILRepacks runtime dependencies into the driver assembly
4. Runs `PatchMergedAssembly.ps1` against the merged assembly
5. Packages the driver into a `.pkg` using Crestron's ManifestUtil

### GitHub Release Asset

This repository includes a GitHub Actions workflow that builds the Release package and attaches the generated `.pkg` to a GitHub Release.

The same release workflow also publishes the `WeatherLinkLiveCrestronDriver` NuGet package, which wraps the final generated `.pkg` artifact.

Typical release flow:
1. Push the release commit and tag
2. Publish the GitHub Release for that tag
3. Let the workflow build and attach the `.pkg` asset automatically

---

## Repository Notes

- XML documentation generation is enabled in the project build
- The release workflow builds the package on `windows-latest`
- The repository includes the driver package/build scripts needed for packaging and deployment

---

## License

MIT + Commons Clause © 2026 Neil Colvin — see [LICENSE](LICENSE).

Free to use and modify. You may not sell the Software as a standalone product or sublicense it.
Commercial system integration work (for example, a Crestron installer commissioning a customer system) is explicitly permitted, even where a fee is charged for that service.

> **Note:** This project references [Crestron.DeviceDrivers.DevKit](https://www.nuget.org/packages/Crestron.DeviceDrivers.DevKit),
> which is subject to Crestron's SDK license agreement. That license governs the SDK libraries only;
> the source code in this repository is licensed independently under the terms above.


## Automated tests

The solution includes `WeatherLinkLiveCrestronDriver.Tests` (NUnit 4 with the Visual Studio NUnit adapter) and `WeatherLinkLiveCrestronDriver.ProcessorTests` (a standalone Crestron Home Utility test package). The offline tests exercise driver logic without credentials or real device commands. The processor lifecycle cases are excluded on Windows in this project; the dedicated desktop SDK harness exercises the same fixture sources.

```powershell
dotnet test WeatherLinkLiveCrestronDriver.Tests/WeatherLinkLiveCrestronDriver.Tests.csproj -c Release
```

Build the processor project in Debug in Visual Studio to build and deploy using private deployment settings. See [processor test instructions](WeatherLinkLiveCrestronDriver.ProcessorTests/README.md) for setup, suites, tile operation and UI separation. Processor packages are not published to NuGet. See [CHANGELOG](CHANGELOG.md) for changes.


### Expanded driver behavior tests

Cover local readings, failure cache retention, cloud refresh throttling and delayed local/cloud responses after configuration changes or disposal. Reject stale responses before they can restore cached weather or online state.

Coordinate overrides must be paired and valid; rejected configuration cannot start network work or replace active location; clearing configuration discards pending edits. The desktop harness injects a synthetic location while the normal constructor still uses the processor location API.

The current package contains offline tests, SDK entity/lifecycle tests and optional live weather station tests. The processor package remains **net472 only**, appears under **Utility** in Configure, and can run independently through its own tile or the Windows NUnit runner. Unit and lifecycle fixtures use synthetic data. Live fixtures read a real station and verify measured readings, repeated refresh and unit selection on a new test entity, without changing station settings.

For live tests, copy `WeatherLinkLiveCrestronDriver.Tests/LiveTestSettings.example.json` to a private `LiveTestSettings.json` and supply `ipAddress`. The desktop SDK harness reads it from `%LOCALAPPDATA%/WeatherLinkLive`, or from the NUnit `TestDataDirectory` parameter. Set `enabled` to `true` for local testing, or supply `EnableLiveTests=true`; `EnableLiveTests=false` always disables it. On the processor, upload the file through **Test inputs** and select **Live Weather Station**. See the [processor test instructions](WeatherLinkLiveCrestronDriver.ProcessorTests/README.md). Keep private inputs outside the repository or exclude them through `.git/info/exclude`; never include them in packages.

`WeatherLinkLiveCrestronDriver.Lifecycle.Tests` runs the entity checks against the real desktop SDK on .NET 10. It compiles the relevant driver sources and shares fixture sources with the net472 processor tests. Building this project does not deploy a driver. A locally supplied `Newtonsoft.Json.Compact.dll` is needed by the SDK's manifest reader; it is supplied by the processor at runtime and must not be added to source control or bundled with the processor test package.

```powershell
dotnet test WeatherLinkLiveCrestronDriver.Tests/WeatherLinkLiveCrestronDriver.Tests.csproj --filter "TestCategory!=Processor"
dotnet test WeatherLinkLiveCrestronDriver.Lifecycle.Tests/WeatherLinkLiveCrestronDriver.Lifecycle.Tests.csproj
```

Set `CompactJsonPath` in the desktop test project's private `DesktopTest.Local.props`, excluded through `.git/info/exclude`, or pass it as an MSBuild property. Keep machine paths and credentials out of tracked files.

Desktop success does not establish Mono compatibility. Build the processor test project in Visual Studio, deploy it, and run both suites on the processor. The fixtures cover configuration, restoration, refresh/reconnect races and disposal using simulated responses. Real installed-driver health and optional live-device checks remain separate from these repeatable suites.


### Driver build and release versions

The driver's JSON manifest is the source of its four-component build version. Debug builds increment only the fourth component; for example, `2.0.001.0005` becomes `2.0.001.0006`. MSBuild's `Version` and default `PackageVersion` are derived from that same manifest and refreshed after the increment; their numeric form is `2.0.1.6`. Assembly binding versions remain separate. Test-only references and IDE design-time builds do not increment the production driver version.

GitHub tags and NuGet releases retain three components: `v2.0.1` and `2.0.1`. Prepare the manifest's first three components for the intended release before tagging. Release CI checks that the tag matches, resets the fourth component to zero, and verifies the generated `.pkg` version against the manifest and release version before publishing. It does not increment the selected patch again. Local Release builds preserve the manifest. A later Debug build can legitimately be newer than a published release; the processor test package has its own independent version.

Deployment validation compares the exact built `.pkg` against the imported catalogue entry and installed instance, numerically including all four components. Upload/import alone does not activate the new version. Keep the tested package and its hash: rebuilding creates a new artifact that must be validated again.

Run `pwsh -File tools/Test-DriverVersioning.ps1` to check these rules with temporary manifests; this does not change the working driver manifest or deploy anything.

See [versioning details](docs/Versioning.md) for build, release and installed-instance verification rules.
### Desktop SDK dependency in CI

The SDK's desktop manifest reader needs `Newtonsoft.Json.Compact.dll`. The public `Crestron.DeviceDrivers.ManifestUtil` 29.0.10 NuGet tool package includes it under `tools/net8.0/any`. Set `CompactJsonPath` to that file (or use private `DesktopTest.Local.props`). CI downloads and verifies this published dependency directly, so its offline tests do not require repository secrets. The DLL is not committed or included in processor packages.


For automated local tests, processor tests and gated driver deployment, see the [Crestron Home NUnit CI development guide](https://github.com/oznetmaster/CrestronHomeNUnit/blob/HEAD/docs/ContinuousIntegration.md). It covers private configuration, live-test gates, install/update waits, results and optional test-package removal.

Local build/deployment overrides can be created by copying [WeatherLinkLiveCrestronDriver.Local.targets.example](WeatherLinkLiveCrestronDriver/WeatherLinkLiveCrestronDriver.Local.targets.example) to `WeatherLinkLiveCrestronDriver.Local.targets` beside the project. Fill in your own paths privately and exclude the resulting local file with `.git/info/exclude`; it is not part of the published source.

## Visual Studio processor workflow

The solution includes [WeatherLinkLiveCrestronDriver.WorkflowTests](WeatherLinkLiveCrestronDriver.WorkflowTests/README.md), using the published Crestron Home Test Adapter. It exposes the complete gated workflow in Test Explorer while the ordinary NUnit fixtures remain available for local testing. Configure its private settings before execution; hosted CI verifies discovery without accessing hardware.

## Publishing when local hardware is unavailable

The publish/release workflows support an explicit manual override when the processor or local self-hosted GitHub Actions runner is unavailable. Select `skip_hardware_checks` and provide a single-line `hardware_skip_reason`. Use the workflow's normal source and version controls. The override applies only to that invocation and is recorded with the exact source revision in its warning and job summary; it does not create a passing hardware-test result.

GitHub-hosted validation remains mandatory for the checked-out source, and the normal build, tests and packaging steps still run. Wait for the configured hosted workflows to pass, or run them on the same source revision first. None of these hosted checks needs the local runner or processor. Automatic tag/release-triggered runs retain the normal hardware checks; use a manual invocation of the updated release workflow when an offline override is needed.
The package includes the [driver help file](WeatherLinkLiveCrestronDriver/IncludeInPkg/NeilColvin_WeatherStation_WeatherLinkLive_IP_V2.pdf) and [third-party licence notices](WeatherLinkLiveCrestronDriver/IncludeInPkg/THIRD-PARTY-NOTICES.txt). The package filename is `NeilColvin_WeatherStation_WeatherLinkLive_IP_V2.pkg`; its existing driver identity is preserved for updates.
