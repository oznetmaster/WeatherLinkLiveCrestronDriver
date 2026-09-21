# WeatherLinkLiveCrestronDriver processor tests

This standalone Entity V2 **Utility** package runs the driver test assembly on Crestron Home's Mono runtime. It has its own identity and NUnit tile. The production driver can remain installed alongside it. The test package does not start or configure the production driver's installed instance.

## Build and deploy

Open `WeatherLinkLiveCrestronDriver.slnx` in Visual Studio with a current .NET SDK, the .NET Framework 4.7.2 targeting pack, and the Crestron Driver SDK installed. Clone [CrestronHomeNUnit](https://github.com/oznetmaster/CrestronHomeNUnit) beside this repository, or set `ProcessorTestSdkRoot` privately. Build **WeatherLinkLiveCrestronDriver.ProcessorTests**, Debug. Enable `DeployAfterBuild` in a private `.csproj.user` file with the same deployment properties as the production driver. Debug deployment is performed only by Visual Studio. Keep credentials and machine paths in files excluded through `.git/info/exclude`; never commit them.

For a command-line package build without deployment:

```powershell
dotnet build WeatherLinkLiveCrestronDriver.ProcessorTests/WeatherLinkLiveCrestronDriver.ProcessorTests.csproj -c Debug -p:BuildProcessorTestPackages=true -p:DeployAfterBuild=false
```

The package appears at `bin/Debug/net472/WeatherLinkLiveCrestronDriver.ProcessorTests.pkg`. Add **WeatherLinkLiveCrestronDriver Tests** from **Utility** in Configure. No separate NUnit host package is required.

## Suites

- **Unit Tests**: Offline driver cases, including pressure-unit conversion. Run on Windows through the NUnit Visual Studio adapter or on the processor. No account credentials or physical devices are needed.
- **Processor Lifecycle**: SDK lifecycle checks, including cloud wind and rainfall conversion. Coordinate overrides must be paired and valid; rejected configuration cannot start network work or replace active location; clearing configuration discards pending edits. The desktop harness injects a synthetic location while the normal constructor still uses the processor location API. Run these separately on the processor; the shared desktop harness provides additional validation.
- **Live Weather Station**: Optional read-only checks against a real WeatherLink Live station. Verify measured temperature and humidity, repeated refresh and unit selection on a newly constructed test entity.
- **Live Cloud Weather**: Optional cloud-only current/daily refresh and cache validation through a new driver test entity. Supply private `CloudTestSettings.json` using the test project's example, containing `apiKey`, `latitude` and `longitude`. It requires no station and does not modify the installed driver; its requests count toward the weather account's usage.

Use the Windows runner's **Find packages**, select this package, connect, then select a suite and **Run all**. Discovery uses a dynamically assigned port. The standalone tile exposes the same suites and results. Nothing runs automatically on deployment. Original driver assets are under `DriverTestData`; the test tile's assets retain their own root paths.

Unit and lifecycle suites use synthetic responses. The live suite reads the configured station without changing station settings. Copy the test project's `LiveTestSettings.example.json` to a private `LiveTestSettings.json`, supply `ipAddress`, and load it through the runner's **Test inputs** before selecting the live suite. Selecting that suite enables it for the run. Keep this file outside the repository or exclude it with `.git/info/exclude`; it is never part of the package.

Live reads are spaced at least ten seconds apart to respect the [Davis local API polling guidance](https://github.com/weatherlink/weatherlink-live-local-api/blob/master/API.md). The three live tests normally need about a minute. They use the real station reader, report underlying read errors, and do not automatically retry failures.

This project targets only `net472`. It is not packable or publishable to NuGet. See [third-party notices](THIRD-PARTY-NOTICES.md), the root LICENSE, and [runner documentation](https://github.com/oznetmaster/CrestronHomeNUnit#readme).


## Expanded coverage

Coordinate overrides must be paired and valid; rejected configuration cannot start network work or replace active location; clearing configuration discards pending edits. The desktop harness injects a synthetic location while the normal constructor still uses the processor location API.

The package contains offline, lifecycle and optional live suites. Lifecycle and live tests exercise newly constructed test entities; checking the installed production instance is a separate workflow stage. Suites are selectable in the Windows runner and through the standalone Utility tile; the live suite requires private inputs uploaded from the runner.


Hosted and release validation compare the exact discovered test identities with execution results and the merged package, rather than maintaining a duplicate expected test count. Live tests are discovered but not operated in hosted CI. Only documented processor-runtime skips are accepted by the Windows net472 check; the desktop SDK harness must execute every automatic test successfully.
