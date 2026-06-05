# WeatherLink Live Library

WeatherLink Live Library is a reusable API client for querying a local WeatherLink Live device.

## Highlights

- Dual-targeted for `net472` and `net10.0`
- Keeps legacy compatibility support isolated to the `net472` build
- Includes DocFX-based documentation support

## Console Test Harness

The solution includes `WeattherLinkLive Test`, a small console program for manual smoke testing.

Before running it, provide the WeatherLink Live device IP using one of these local-only options:

- `WEATHERLINK_LIVE_IP` environment variable
- `.local/weatherlink-live-ip.txt` in the solution root, excluded via the repository-local `.git/info/exclude` file

## Build

```powershell
dotnet build .\WeatherLink Live Library.sln -c Release
```

## Release

Releases are prepared through GitHub Actions:

- `publish-docfx.yml` publishes the documentation site to GitHub Pages
- `release-nuget.yml` builds the release package and creates a GitHub release from tags

## Documentation

- [Published API Documentation](https://oznetmaster.github.io/WeatherLinkLiveLibrary/)

## Notes

- No personal, localized, or machine-specific values are published in this repository-facing documentation.
