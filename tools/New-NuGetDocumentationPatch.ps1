# Copyright (c) 2026 Neil Colvin.
# Licensed under the MIT License with Commons Clause. See LICENSE.
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$BaseVersion,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$base = [version]$BaseVersion
$next = [version]$Version
if ($next.Major -ne $base.Major -or $next.Minor -ne $base.Minor -or $next.Build -ne $base.Build + 1) { throw 'Use the next patch version of the existing package.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh output directory.' }
$root = Split-Path $PSScriptRoot -Parent
$id = 'CrestronHomeDriver.WeatherLinkLive.WeatherStation'
$payload = 'NeilColvin_WeatherStation_WeatherLinkLive_IP_V2.pkg'
$stage = Join-Path $OutputDirectory 'input'
[IO.Directory]::CreateDirectory($stage) | Out-Null
$download = Join-Path $OutputDirectory 'base.nupkg'
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/$BaseVersion/$($id.ToLowerInvariant()).$BaseVersion.nupkg" -OutFile $download
$zip = [IO.Compression.ZipFile]::OpenRead($download)
try {
    foreach ($name in @('LICENSE', $payload, 'crestron-driver-package.json', "$id.nuspec")) {
        $entry = $zip.GetEntry($name)
        if (!$entry) { throw "Missing expected base-package file: $name" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $stage $name))
    }
} finally { $zip.Dispose() }
$payloadHash = (Get-FileHash (Join-Path $stage $payload) -Algorithm SHA256).Hash
Copy-Item -LiteralPath "$root/README.md" -Destination "$stage/README.md"
$readme = Get-Content "$stage/README.md" -Raw
if ([regex]::IsMatch($readme, '\]\((?!https?://|#|mailto:)[^)]+\)')) { throw 'NuGet README contains a relative link.' }
$manifestPath = Join-Path $stage 'crestron-driver-package.json'
$descriptor = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($descriptor.packageId -ne $id -or $descriptor.packageVersion -ne $BaseVersion -or $descriptor.payloadFile -ne $payload) { throw 'Base package descriptor does not match the requested package.' }
$descriptor.packageVersion = $Version
$descriptor | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding utf8
$nuspecPath = Join-Path $stage "$id.nuspec"
[xml]$nuspec = Get-Content $nuspecPath -Raw
if ($nuspec.package.metadata.id -ne $id -or $nuspec.package.metadata.version -ne $BaseVersion) { throw 'Unexpected base package identity.' }
$nuspec.package.metadata.version = $Version
$nuspec.package.metadata.releaseNotes = "Documentation-only NuGet patch: correct the README license link and processor-test package version (1.2.1), and describe recovery as released. The driver .pkg is byte-for-byte unchanged from NuGet $BaseVersion. See the GitHub driver release for runtime changes."
$nuspec.package.metadata.repository.commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot record documentation source revision.' }
$files = $nuspec.CreateElement('files', $nuspec.DocumentElement.NamespaceURI)
foreach ($name in @('LICENSE', 'README.md', $payload, 'crestron-driver-package.json')) {
    $file = $nuspec.CreateElement('file', $nuspec.DocumentElement.NamespaceURI)
    $file.SetAttribute('src', $name); $file.SetAttribute('target', $name)
    [void]$files.AppendChild($file)
}
[void]$nuspec.DocumentElement.AppendChild($files)
$nuspec.Save($nuspecPath)
& nuget pack $nuspecPath -BasePath $stage -OutputDirectory $OutputDirectory -NonInteractive
if ($LASTEXITCODE -ne 0) { throw 'Documentation packaging failed.' }
$published = [IO.Compression.ZipFile]::OpenRead((Join-Path $OutputDirectory "$id.$Version.nupkg"))
try {
    $stream = $published.GetEntry($payload).Open()
    try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
    if ($actual -ne $payloadHash) { throw 'Driver payload changed during documentation packaging.' }
} finally { $published.Dispose() }
Write-Host "Verified documentation-only NuGet $Version; driver payload SHA256 $payloadHash."
