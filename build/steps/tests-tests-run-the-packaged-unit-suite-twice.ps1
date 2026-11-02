$output = Join-Path $PWD 'WeatherLinkLiveCrestronDriver.ProcessorTests/bin/Release/net472'
$extracted = Join-Path $env:RUNNER_TEMP 'driver-package-tests'
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $output 'WeatherLinkLiveCrestronDriver.ProcessorTests.pkg'), $extracted)
./tools/Test-DiscoveredCoverage.ps1 -Stage Package -SdkRoot ../CrestronHomeNUnit -PackageAssembly (Join-Path $extracted 'WeatherLinkLiveCrestronDriver.ProcessorTests.dll') -SourceInventory TestResults/WeatherLinkLiveCrestronDriver.Lifecycle.Tests/inventory.json -ResultsDirectory TestResults
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
