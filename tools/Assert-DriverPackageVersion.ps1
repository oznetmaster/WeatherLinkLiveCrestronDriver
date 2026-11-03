# Copyright (c) 2026 Neil Colvin.
# Licensed under the MIT License with Commons Clause; see repository LICENSE.
param([Parameter(Mandatory)][string]$PackagePath,[Parameter(Mandatory)][string]$ManifestPath,[Parameter(Mandatory)][string]$ReleaseVersion,[string]$ProductAssetsDirectory)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/../build/shared/driver-versioning/Assert-DriverPackageVersion.ps1" -PackagePath $PackagePath -ManifestPath $ManifestPath -ReleaseVersion $ReleaseVersion
if (!$ProductAssetsDirectory) { return }
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    if ($ProductAssetsDirectory) {
        $names = @($zip.Entries | ForEach-Object FullName)
        if (@($names | Where-Object { $_.Contains('\') }).Count -or @($names | Sort-Object -Unique).Count -ne $names.Count) {
            throw 'Package contains backslash or duplicate entry names.'
        }
        $helpName = [IO.Path]::GetFileNameWithoutExtension($PackagePath) + '.pdf'
        foreach ($name in @($helpName, 'THIRD-PARTY-NOTICES.txt')) {
            $source = Join-Path $ProductAssetsDirectory $name
            $entry = $zip.GetEntry($name)
            if (!(Test-Path -LiteralPath $source -PathType Leaf) -or $null -eq $entry) { throw "Missing product document: $name" }
            $stream = $entry.Open()
            $hash = [Security.Cryptography.SHA256]::Create()
            try { $actual = [Convert]::ToHexString($hash.ComputeHash($stream)) }
            finally { $hash.Dispose(); $stream.Dispose() }
            if ($actual -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw "Packaged product document differs from source: $name" }
        }
    }
} finally { $zip.Dispose() }
