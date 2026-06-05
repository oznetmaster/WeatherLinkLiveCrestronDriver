# SimpleWeatherClient

SimpleWeatherClient is a Windows solution built around the `SimpleWeather` library for working with the OpenWeather APIs.

This repository contains:
- the `SimpleWeather` reusable API library
- a WPF desktop client
- a WinUI widget client
- a small console-based test harness

> Website: https://openweathermap.org/

## Original Project and Attribution

This solution is adapted from the original [`Banovvv/SimpleWeather`](https://github.com/Banovvv/SimpleWeather) project by **Ivan Gechev** and continues to respect the original MIT licensing and attribution requirements.

The original project was a .NET 6 weather library. This repository extends that foundation into a broader solution with additional applications, updated targeting, local configuration improvements, and ongoing modernization work.

## What Is Different in This Adaptation

Compared with the original upstream project, this repository currently differs in several important ways:

- **Broader solution structure**
  - The repo is no longer just a single library project.
  - It now includes multiple applications built around the shared `SimpleWeather` library.

- **Dual-targeted library**
  - `SimpleWeather` targets both:
    - `.NET Framework 4.7.2`
    - `.NET 10`
  - This allows the API library to be used from both legacy and modern .NET applications.
  - The `.NET Framework 4.7.2` target still uses the latest C# language version.
  - Compatibility packages and shims are included so the legacy target can support newer language/runtime-facing features used alongside the `.NET 10` target.

- **Additional Windows clients**
  - `SimpleWeather.Desktop` is a WPF desktop application.
  - `SimpleWeather.Widget` is a WinUI-based widget-style client.

- **Safer local API-key handling**
  - Real API keys are not intended to be stored in tracked repository files.
  - The apps can fall back to a local non-published key file for local development and testing.

- **Expanded documentation groundwork**
  - XML documentation is enabled for the `SimpleWeather` library.
  - DocFX assets are included for generating API documentation.

- **Solution-level modernization work**
  - Nullable reference types and newer C# features are in use in the modernized code.
  - The solution is being prepared for cleaner GitHub publication and NuGet packaging workflows.

## Solution Structure

- `SimpleWeather/` - shared weather API library
- `SimpleWeather.Desktop/` - WPF desktop app
- `SimpleWeather.Widget/` - WinUI widget client
- `SimpleWeatherTest/` - console test application
- `docs/` - repo-facing markdown documentation
- `docfx/` - API documentation assets and generated content

## Features

### `SimpleWeather` library
- strongly typed weather models
- current weather retrieval
- forecast retrieval
- geolocation helpers
- support for OpenWeather-based weather queries from reusable .NET code

### Desktop and widget clients
- weather lookup from Windows UI applications
- shared use of the `SimpleWeather` library
- local, non-published API-key fallback support for development use

## Documentation

If you are looking for the actual API surface documentation for the `SimpleWeather` library, start here:

- [Published API Documentation Site](https://oznetmaster.github.io/SimpleWeatherClient/)
- [API Documentation Guide](docs/api.md)

Additional repository and contributor documentation:

- [Overview](docs/overview.md)
- [Configuration](docs/configuration.md)
- [Library](docs/library.md)
- [Desktop Application](docs/desktop.md)
- [Widget Application](docs/widget.md)
- [Releases and Packages](docs/releases.md)

The repository docs in `docs/` are for contributors and GitHub readers.
The generated API/reference documentation is built from XML comments and DocFX assets under `docfx/` and published to GitHub Pages.

## Requirements

To build and run the full solution on Windows, you will typically want:
- Visual Studio 2026 or later with .NET desktop development tools
- .NET 10 SDK
- .NET Framework 4.7.2 targeting pack / developer tools
- Windows 10/11 for the desktop and widget clients

## Local Configuration

This repository is set up so a tracked `App.config` can contain only placeholders while local development still works.

### API key lookup order

The desktop app and test app check for an OpenWeather API key in this order:

1. `App.config`
2. solution-local secret file: `.local/openweather-api-key.txt`
3. local AppData fallback: `%AppData%\SimpleWeather\desktop-api-key.txt`

### Recommended local setup

For portable local development, create this file in the solution root:

`.local/openweather-api-key.txt`

Its contents should be only the raw API key, for example:

```text
YOUR_OPENWEATHER_API_KEY_HERE
```

That `.local` folder is intended to remain local and non-published.

## Building the Solution

From the repository root:

```powershell
dotnet build .\SimpleWeather.sln
```

Or build from Visual Studio.

## Running the Projects

- `SimpleWeather.Desktop` - desktop UI client
- `SimpleWeather.Widget` - widget-style Windows client
- `SimpleWeatherTest` - console-based smoke test / development harness

Before running applications that call the OpenWeather service, make sure a valid API key is available through one of the supported local configuration paths.

## Planned Publishing Workflow

This repository is set up for:
- GitHub publication under the `SimpleWeatherClient` name
- automated NuGet package creation
- release-based package publishing

## License

This repository includes MIT-licensed upstream work and preserves attribution to the original author where required.

See:
- `LICENSE`
- `SimpleWeather/LICENSE`
- project source headers in adapted files

## Acknowledgments

- Original upstream project: [`Banovvv/SimpleWeather`](https://github.com/Banovvv/SimpleWeather)
- Original author: **Ivan Gechev**
- Current adaptation and expansion: **Neil Colvin**
