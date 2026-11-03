# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $env:TEMP ('WeatherVersionExtension-'+[guid]::NewGuid().ToString('N'))
$null=[IO.Directory]::CreateDirectory($fixture)
$checks=0
try {
    $manifest=Join-Path $fixture 'manifest.json'
    @{GeneralInformation=@{DriverVersion='2.0.001.0000'}}|ConvertTo-Json -Depth 4|Set-Content $manifest
    $package=Join-Path $fixture 'Example.pkg'
    $docs=Join-Path $fixture 'documents';$null=[IO.Directory]::CreateDirectory($docs)
    [IO.File]::WriteAllText((Join-Path $docs 'Example.pdf'),'synthetic submission-help fixture')
    [IO.File]::WriteAllText((Join-Path $docs 'THIRD-PARTY-NOTICES.txt'),'synthetic notice fixture')
    function New-Package([switch]$Missing,[switch]$Changed,[switch]$Duplicate,[switch]$Backslash) {
        if(Test-Path $package){Remove-Item -LiteralPath $package}
        $zip=[IO.Compression.ZipFile]::Open($package,[IO.Compression.ZipArchiveMode]::Create)
        try {
            $entries=@{ 'Example.dat'='{"driverVersion":"2.0.1.0"}';'THIRD-PARTY-NOTICES.txt'='synthetic notice fixture' }
            if(!$Missing){$entries['Example.pdf']=if($Changed){'modified'}else{'synthetic submission-help fixture'}}
            if($Backslash){$entries['unexpected\file.txt']='x'}
            foreach($entry in $entries.GetEnumerator()){
                $writer=[IO.StreamWriter]::new($zip.CreateEntry($entry.Key).Open());try{$writer.Write($entry.Value)}finally{$writer.Dispose()}
            }
            if($Duplicate){$writer=[IO.StreamWriter]::new($zip.CreateEntry('Example.pdf').Open());try{$writer.Write('duplicate')}finally{$writer.Dispose()}}
        } finally {$zip.Dispose()}
    }
    function Check([bool]$Reject) {
        $failed=$false
        try{& "$root/tools/Assert-DriverPackageVersion.ps1" -PackagePath $package -ManifestPath $manifest -ReleaseVersion '2.0.1' -ProductAssetsDirectory $docs}catch{$failed=$true}
        if($failed -ne $Reject){throw 'Unexpected WeatherLink submission document assertion outcome.'}
        $script:checks++
    }
    New-Package;Check $false
    New-Package -Missing;Check $true
    New-Package -Changed;Check $true
    New-Package -Duplicate;Check $true
    New-Package -Backslash;Check $true
    "Passed $checks WeatherLink submission-extension checks."
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    if([IO.Path]::GetDirectoryName($resolved).TrimEnd('\','/') -ne [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\','/') -or [IO.Path]::GetFileName($resolved) -notlike 'WeatherVersionExtension-*'){throw 'Unexpected fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
