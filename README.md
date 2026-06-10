# WeatherLinkLiveCrestronDriver

A **Crestron Home** extension driver that integrates a local **WeatherLink Live™** device for current conditions and uses **OpenWeather** cloud data for forecast information and fallback current conditions when the local device is unavailable.

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
| OpenWeather API key | Required for forecast data and cloud fallback current conditions |
| Crestron Home system location | Must be configured for cloud weather requests |

---

## Installation

Preferred download source: use the attached `.pkg` file from the relevant GitHub Release. The automatic GitHub `Source code (zip)` and `Source code (tar.gz)` assets are repository snapshots, not installable Crestron driver packages.

NuGet package availability: this driver is also published as the `CrestronHomeDriver.WeatherLinkLive.WeatherStation` NuGet package. This NuGet package conforms to the **Crestron Home Driver NuGet Publishing Standard v1**. It is a distribution wrapper for the final `.pkg` artifact, includes the required `crestron-driver-package.json` manifest, and is not intended as a direct DLL reference package.

Crestron Home Driver NuGet Publishing Standard v1 is **not** an official Crestron product or specification. It is an open source packaging standard created to facilitate community distribution and discovery of Crestron Home drivers through NuGet.

1. Download the generated `.pkg` asset from the GitHub Release, or build it yourself using the instructions in [Building from Source](#building-from-source).
2. Upload the `.pkg` file to your Crestron Home processor manually (for example via SFTP to `/user/ThirdPartyDrivers/Import`).
3. In the Crestron Home configuration UI, add a new device and select the **WeatherLink Live Weather Station** driver.
4. Configure the driver:

| Field | Description |
|---|---|
| WeatherLink Live Host | Optional. IP address or hostname of the local WeatherLink Live device |
| OpenWeather API Key | Required. Used for forecast data and cloud fallback current conditions |
| Location Name Override | Optional. Overrides the title location name shown on the current conditions page |
| Latitude Override | Optional. Leave blank to use the Crestron Home system latitude for cloud weather requests |
| Longitude Override | Optional. Leave blank to use the Crestron Home system longitude for cloud weather requests |
| Units | `Metric`, `UK`, or `Imperial` |
| Refresh Interval Seconds | Refresh interval for scheduled current-condition updates; forecast/cloud refreshes follow their own startup, manual, and daily refresh rules |

If a WeatherLink Live host is supplied, the driver prefers it for current conditions. If it is unavailable at a given refresh, the driver can fall back to cached/throttled cloud weather data.

The current conditions and weekly forecast page title locations use the following priority order:

1. **Location Name Override**
2. City name returned by the OpenWeather current weather response
3. Reverse-geocoded city name from the configured/effective coordinates
4. Effective latitude/longitude text

Latitude and longitude overrides are optional, but they must both be supplied together. When left blank, the driver uses the Crestron Home system location for cloud weather access.

---

## Current and Forecast Refresh Behavior

- **Startup:** current conditions refresh immediately, and cloud forecast data is also initialized so forecast-related fields are populated
- **Scheduled updates:** current conditions refresh on the configured interval
- **Forecast button:** the forecast page can request a cloud refresh when needed
- **Cloud throttling:** normal cloud requests are limited to once every 10 minutes, except for the daily post-00:01 refresh trigger

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
- `ManifestUtil.exe` from the Crestron Driver SDK to produce the final `.pkg`

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

For end users, GitHub Releases are the preferred download point: download the attached `.pkg` asset, not the automatic source archive assets.

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

WeatherLink Live™ is a trademark of Davis Instruments.

> **Note:** This project references [Crestron.DeviceDrivers.DevKit](https://www.nuget.org/packages/Crestron.DeviceDrivers.DevKit),
> which is subject to Crestron's SDK license agreement. That license governs the SDK libraries only;
> the source code in this repository is licensed independently under the terms above.